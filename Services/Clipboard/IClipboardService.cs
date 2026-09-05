using System;

namespace PasswordManager.Services.Clipboard;

/// <summary>
/// Service interface for managing clipboard operations with security features like auto-clearing sensitive data.
/// </summary>
public interface IClipboardService : IDisposable
{
    /// <summary>
    /// Default timeout duration for sensitive clipboard clearing.
    /// </summary>
    TimeSpan DefaultTimeout { get; set; }

    /// <summary>
    /// Event triggered when sensitive data is cleared from the clipboard automatically or manually.
    /// </summary>
    event Action<string>? ClipboardCleared;

    /// <summary>
    /// Copies standard text (e.g. username, URL) to the system clipboard without an auto-clear timer.
    /// </summary>
    /// <param name="text">The text to copy to the clipboard.</param>
    void CopyToClipboard(string text);

    /// <summary>
    /// Copies sensitive text (e.g. password) to the clipboard and schedules automatic clearing after the specified timeout.
    /// </summary>
    /// <param name="text">The sensitive text to copy.</param>
    /// <param name="timeout">Optional custom timeout; defaults to DefaultTimeout (30s).</param>
    void CopySensitiveToClipboard(string text, TimeSpan? timeout = null);

    /// <summary>
    /// Gets current text from the clipboard, or null if clipboard contains non-text data or is inaccessible.
    /// </summary>
    string? GetText();

    /// <summary>
    /// Clears the clipboard if it currently contains the expected sensitive text.
    /// </summary>
    /// <param name="expectedText">The text that must match the current clipboard content to trigger clearing.</param>
    /// <returns>True if the clipboard was cleared; false if content changed externally or was already cleared.</returns>
    bool ClearIfMatches(string expectedText);

    /// <summary>
    /// Immediately clears the clipboard regardless of content.
    /// </summary>
    void ClearClipboard();
}
