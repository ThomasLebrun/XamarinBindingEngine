using System;
using System.Globalization;
using BindingEngine.Android.Converters;

namespace SampleBindingEngine.Core
{
    public class BooleanToStringConverter : IBindingValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool result;

            bool.TryParse(value.ToString(), out result);

            return result;
        }
    }
}