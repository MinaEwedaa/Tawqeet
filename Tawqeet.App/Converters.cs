using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Tawqeet.App;

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToUpperInvariant();
        return status switch
        {
            "IN" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),   // Green
            "OUT" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),  // Red
            "PRESENT" => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
            "LATE" => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Orange
            "ABSENT" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
            _ => new SolidColorBrush(Color.FromRgb(107, 114, 128))     // Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class StatusToBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToUpperInvariant();
        return status switch
        {
            "IN" => new SolidColorBrush(Color.FromRgb(220, 252, 231)),   // Light Green
            "OUT" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),  // Light Red
            "PRESENT" => new SolidColorBrush(Color.FromRgb(220, 252, 231)),
            "LATE" => new SolidColorBrush(Color.FromRgb(254, 243, 199)), // Light Orange
            "ABSENT" => new SolidColorBrush(Color.FromRgb(254, 226, 226)),
            _ => new SolidColorBrush(Color.FromRgb(243, 244, 246))       // Light Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Visible;
    }
}

public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is Visibility.Collapsed;
    }
}

public class BoolToConnectTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Disconnect" : "Connect";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}






