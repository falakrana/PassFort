using System.Windows;
using System.Windows.Controls;

namespace PasswordManager.Helpers;

/// <summary>
/// Attached DependencyProperty helper for WPF PasswordBox to enable MVVM data binding.
/// </summary>
public static class PasswordBoxHelper
{
    public static readonly DependencyProperty BoundPasswordProperty =
        DependencyProperty.RegisterAttached(
            "BoundPassword",
            typeof(string),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, OnBoundPasswordChanged));

    public static readonly DependencyProperty AttachProperty =
        DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false, OnAttachChanged));

    private static readonly DependencyProperty IsUpdatingProperty =
        DependencyProperty.RegisterAttached(
            "IsUpdating",
            typeof(bool),
            typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(false));

    public static string GetBoundPassword(DependencyObject obj)
    {
        return (string)obj.GetValue(BoundPasswordProperty);
    }

    public static void SetBoundPassword(DependencyObject obj, string value)
    {
        obj.SetValue(BoundPasswordProperty, value);
    }

    public static bool GetAttach(DependencyObject obj)
    {
        return (bool)obj.GetValue(AttachProperty);
    }

    public static void SetAttach(DependencyObject obj, bool value)
    {
        obj.SetValue(AttachProperty, value);
    }

    private static bool GetIsUpdating(DependencyObject obj)
    {
        return (bool)obj.GetValue(IsUpdatingProperty);
    }

    private static void SetIsUpdating(DependencyObject obj, bool value)
    {
        obj.SetValue(IsUpdatingProperty, value);
    }

    private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;

        if ((bool)e.OldValue)
        {
            passwordBox.PasswordChanged -= PasswordChanged;
        }

        if ((bool)e.NewValue)
        {
            passwordBox.PasswordChanged += PasswordChanged;
        }
    }

    private static void PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not PasswordBox passwordBox) return;

        SetIsUpdating(passwordBox, true);
        SetBoundPassword(passwordBox, passwordBox.Password);
        SetIsUpdating(passwordBox, false);
    }

    private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not PasswordBox passwordBox) return;

        if (GetIsUpdating(passwordBox)) return;

        passwordBox.Password = (string)e.NewValue ?? string.Empty;
    }
}
