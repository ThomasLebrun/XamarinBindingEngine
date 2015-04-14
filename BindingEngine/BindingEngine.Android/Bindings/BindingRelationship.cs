using System.Reflection;
using Android.Views;
using BindingEngine.Android.Converters;

namespace BindingEngine.Android.Bindings
{
    public class BindingRelationship
    {
        public PropertyInfo SourceProperty { get; set; }
        public PropertyInfo TargetProperty { get; set; }
        public IBindingValueConverter Converter { get; set; }
        public string ConverterParameter { get; set; }
        public View Control { get; set; }
    }
}