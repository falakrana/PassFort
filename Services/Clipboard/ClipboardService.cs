using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace PasswordManager.Services.Clipboard;

/// <summary>
/// Production implementation of IClipboardService with STA WPF clipboard access and auto-clear security timers.
/// </summary>
public class ClipboardService : IClipboardService
{
    private readonly object _lock = new();
    private Timer? _clearTimer;
    private string? _lastCopiedSensitiveText;
    private bool _disposed;

    public ClipboardService()
    {
        DefaultTimeout = TimeSpan.FromSeconds(30);
    }

    public TimeSpan DefaultTimeout { get; set; }

    public event Action<string>? ClipboardCleared;

    public void CopyToClipboard(string text)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        lock (_lock)
        {
            CancelTimer();
            _lastCopiedSensitiveText = null;
        }

        SetClipboardTextInternal(text);
    }

    public void CopySensitiveToClipboard(string text, TimeSpan? timeout = null)
    {
        if (text == null) throw new ArgumentNullException(nameof(text));

        var effectiveTimeout = timeout ?? DefaultTimeout;

        lock (_lock)
        {
            CancelTimer();
            _lastCopiedSensitiveText = text;

            SetClipboardTextInternal(text);

            if (effectiveTimeout > TimeSpan.Zero)
            {
                _clearTimer = new Timer(
                    onTimerElapsed,
                    text,
                    (int)effectiveTimeout.TotalMilliseconds,
                    Timeout.Infinite);
            }
        }
    }

    public string? GetText()
    {
        return GetClipboardTextInternal();
    }

    public bool ClearIfMatches(string expectedText)
    {
        if (string.IsNullOrEmpty(expectedText)) return false;

        lock (_lock)
        {
            var currentText = GetText();
            if (currentText == expectedText)
            {
                ClearClipboardInternal();
                if (_lastCopiedSensitiveText == expectedText)
                {
                    _lastCopiedSensitiveText = null;
                }
                CancelTimer();
                ClipboardCleared?.Invoke(expectedText);
                return true;
            }
        }

        return false;
    }

    public void ClearClipboard()
    {
        lock (_lock)
        {
            var previousSensitive = _lastCopiedSensitiveText;
            ClearClipboardInternal();
            _lastCopiedSensitiveText = null;
            CancelTimer();
            if (previousSensitive != null)
            {
                ClipboardCleared?.Invoke(previousSensitive);
            }
        }
    }

    private void onTimerElapsed(object? state)
    {
        if (state is string expectedSensitiveText)
        {
            ClearIfMatches(expectedSensitiveText);
        }
    }

    private void CancelTimer()
    {
        _clearTimer?.Dispose();
        _clearTimer = null;
    }

    protected virtual void SetClipboardTextInternal(string text)
    {
        ExecuteOnStaThread(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    break;
                }
            }
        });
    }

    protected virtual string? GetClipboardTextInternal()
    {
        string? result = null;
        ExecuteOnStaThread(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        result = System.Windows.Clipboard.GetText();
                    }
                    return;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    break;
                }
            }
        });
        return result;
    }

    protected virtual void ClearClipboardInternal()
    {
        ExecuteOnStaThread(() =>
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.Windows.Clipboard.Clear();
                    return;
                }
                catch (ExternalException)
                {
                    Thread.Sleep(50);
                }
                catch (Exception)
                {
                    break;
                }
            }
        });
    }

    private static void ExecuteOnStaThread(Action action)
    {
        if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
        {
            action();
        }
        else
        {
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch
                {
                    // Ignore non-fatal clipboard thread execution errors
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                lock (_lock)
                {
                    CancelTimer();
                    _lastCopiedSensitiveText = null;
                }
            }
            _disposed = true;
        }
    }
}
