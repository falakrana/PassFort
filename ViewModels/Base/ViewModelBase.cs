using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PasswordManager.ViewModels.Base;

/// <summary>
/// Abstract base class for all ViewModels in the application.
/// Implements INotifyPropertyChanged to enable WPF data binding updates.
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event for a specified property.
    /// </summary>
    /// <param name="propertyName">Name of the property that changed. Automatically supplied if omitted.</param>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Sets a backing field to a new value and raises PropertyChanged if the value changed.
    /// </summary>
    /// <typeparam name="T">The type of the property.</typeparam>
    /// <param name="storage">Reference to the backing field.</param>
    /// <param name="value">The new value to set.</param>
    /// <param name="propertyName">Name of the property. Automatically supplied if omitted.</param>
    /// <returns>True if the value was modified; otherwise false.</returns>
    protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        storage = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
