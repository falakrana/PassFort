using System;
using System.Threading;
using PasswordManager.Services.Authentication;

namespace PasswordManager.Services.AutoLock;

/// <summary>
/// Monitors user inactivity when the vault is unlocked, resetting timers on user activity
/// and automatically locking the vault upon timeout.
/// </summary>
public class AutoLockService : IAutoLockService, IDisposable
{
    private readonly IAuthenticationService _authService;
    private readonly object _timerLock = new();

    private Timer? _timer;
    private TimeSpan _timeout = TimeSpan.FromMinutes(5);
    private bool _isEnabled = true;
    private bool _isRunning;
    private bool _disposed;

    public AutoLockService(IAuthenticationService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _authService.LockStateChanged += OnLockStateChanged;

        // Synchronize initial state if vault is already unlocked
        if (_authService.IsUnlocked)
        {
            Start();
        }
    }

    public TimeSpan Timeout
    {
        get
        {
            lock (_timerLock)
            {
                return _timeout;
            }
        }
        set
        {
            if (value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Timeout must be greater than zero.");
            }

            lock (_timerLock)
            {
                _timeout = value;
                if (_isRunning && _isEnabled)
                {
                    ResetTimer();
                }
            }
        }
    }

    public bool IsEnabled
    {
        get
        {
            lock (_timerLock)
            {
                return _isEnabled;
            }
        }
        set
        {
            lock (_timerLock)
            {
                _isEnabled = value;
                if (!_isEnabled)
                {
                    StopInternal();
                }
                else if (_authService.IsUnlocked)
                {
                    StartInternal();
                }
            }
        }
    }

    public bool IsRunning
    {
        get
        {
            lock (_timerLock)
            {
                return _isRunning;
            }
        }
    }

    public event EventHandler? AutoLocked;

    public void Start()
    {
        lock (_timerLock)
        {
            StartInternal();
        }
    }

    public void Stop()
    {
        lock (_timerLock)
        {
            StopInternal();
        }
    }

    public void RegisterActivity()
    {
        lock (_timerLock)
        {
            if (!_disposed && _isRunning && _isEnabled && _authService.IsUnlocked)
            {
                ResetTimer();
            }
        }
    }

    private void StartInternal()
    {
        if (_disposed || !_isEnabled) return;

        _timer?.Dispose();
        _timer = new Timer(OnTimerElapsed, null, _timeout, System.Threading.Timeout.InfiniteTimeSpan);
        _isRunning = true;
    }

    private void StopInternal()
    {
        _timer?.Dispose();
        _timer = null;
        _isRunning = false;
    }

    private void ResetTimer()
    {
        _timer?.Change(_timeout, System.Threading.Timeout.InfiniteTimeSpan);
    }

    private void OnTimerElapsed(object? state)
    {
        lock (_timerLock)
        {
            if (_disposed || !_isRunning || !_isEnabled) return;

            StopInternal();
        }

        // Lock vault if currently unlocked
        if (_authService.IsUnlocked)
        {
            _authService.Lock();
            AutoLocked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnLockStateChanged()
    {
        lock (_timerLock)
        {
            if (_authService.IsUnlocked && _isEnabled)
            {
                StartInternal();
            }
            else
            {
                StopInternal();
            }
        }
    }

    public void Dispose()
    {
        lock (_timerLock)
        {
            if (!_disposed)
            {
                _disposed = true;
                _authService.LockStateChanged -= OnLockStateChanged;
                StopInternal();
            }
        }
    }
}
