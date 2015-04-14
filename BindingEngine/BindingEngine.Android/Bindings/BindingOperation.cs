using Android.Views;

namespace BindingEngine.Android.Bindings
{
    public class BindingOperation
    {
        public View Control { get; set; }
        public string Source { get; set; }
        public string Target { get; set; }
        public string Converter { get; set; }
        public string ConverterParameter { get; set; }
        public BindingMode Mode { get; set; }
    }
}