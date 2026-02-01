using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ChitterChatterClient.Models;

namespace ChitterChatterClient.Converters;

/// <summary>
/// Converts boolean to Visibility (true = Visible, false = Collapsed).
/// </summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) ?? false;
        var boolValue = value is bool b && b;

        if (invert) boolValue = !boolValue;

        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility v && v == Visibility.Visible;
    }
}

/// <summary>
/// Converts ConnectionState to status colour.
/// </summary>
public sealed class ConnectionStateToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            ConnectionState.Connected => new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)), // Green
            ConnectionState.Connecting or ConnectionState.Reconnecting => new SolidColorBrush(Color.FromRgb(0xfa, 0x77, 0x33)), // Orange
            ConnectionState.Failed => new SolidColorBrush(Color.FromRgb(0xdb, 0x59, 0x2e)), // Red
            _ => new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)) // Grey
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts UserStatus to status colour.
/// </summary>
public sealed class UserStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            UserStatus.Online => new SolidColorBrush(Color.FromRgb(0x22, 0xc5, 0x5e)), // Green
            UserStatus.InRoom => new SolidColorBrush(Color.FromRgb(0x1d, 0x97, 0xa6)), // IF Light
            UserStatus.InPrivateCall => new SolidColorBrush(Color.FromRgb(0xfa, 0x77, 0x33)), // Orange
            UserStatus.Away => new SolidColorBrush(Color.FromRgb(0xfa, 0xa2, 0x36)), // Yellow
            _ => new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)) // Grey
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts UserStatus to status text.
/// </summary>
public sealed class UserStatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            UserStatus.Online => "Online",
            UserStatus.InRoom => "In Room",
            UserStatus.InPrivateCall => "In Call",
            UserStatus.Away => "Away",
            UserStatus.Offline => "Offline",
            _ => "Unknown"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to mute icon text.
/// </summary>
public sealed class MuteToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? "🔇" : "🎤";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts boolean to deafen icon text.
/// </summary>
public sealed class DeafenToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? "🔇" : "🔊";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts float (0-1) to percentage width for level meter.
/// </summary>
public sealed class LevelToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is float f)
        {
            var maxWidth = parameter is double d ? d : 100.0;
            return Math.Min(f * maxWidth, maxWidth);
        }
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Converts speaking status to glow effect.
/// </summary>
public sealed class SpeakingToGlowConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isSpeaking && isSpeaking)
        {
            return new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0x22, 0xc5, 0x5e),
                BlurRadius = 15,
                ShadowDepth = 0,
                Opacity = 0.8
            };
        }
        return null;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverts a boolean value.
/// </summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && !b;
    }
}

/// <summary>
/// Returns Visibility.Visible if value is not null.
/// </summary>
public sealed class NotNullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var invert = parameter?.ToString()?.Equals("invert", StringComparison.OrdinalIgnoreCase) ?? false;
        var isNotNull = value is not null;

        if (invert) isNotNull = !isNotNull;

        return isNotNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
