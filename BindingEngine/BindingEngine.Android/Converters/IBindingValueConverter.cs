using System;
using System.Globalization;

namespace BindingEngine.Android.Converters
{
    public interface IBindingValueConverter
    {
        object Convert(object value, Type targetType, object parameter, CultureInfo culture);

        object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture);
    }
}