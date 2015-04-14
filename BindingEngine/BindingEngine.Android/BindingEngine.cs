using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Xml.Linq;
using Android;
using Android.Views;
using Android.Widget;
using BindingEngine.Android.Bindings;
using BindingEngine.Android.Converters;
using BindingEngine.Android.Views;
using Java.Lang;
using Switch = Android.Widget.Switch;

namespace BindingEngine.Android
{
    public class BindingEngine
    {
        //private const string DataContextPropertyName = "DataContext";
        private const string ViewLayoutResourceIdPropertyName = "ViewLayoutResourceId";

        private static readonly XName BindingOperationXmlNamespace = XNamespace.Get("http://schemas.android.com/apk/res-auto") + "Binding";

        private static bool _preventUpdateForTargetProperty;
        private static bool _preventUpdateForSourceProperty;

        private static readonly List<Type> ValueConverters;

        static BindingEngine()
        {
            ValueConverters = new List<Type>();
        }

        public static void Initialize<TViewModel>(BindingActivity<TViewModel> bindingActivity) where TViewModel : BindableObject
        {
            List<View> viewElements = null;
            List<XElement> xmlElements = null;

            // Find the value of the ViewLayoutResourceId property
            var viewLayoutResourceIdProperty = bindingActivity.GetType().GetProperty(ViewLayoutResourceIdPropertyName);
            var viewLayoutResourceId = (int)viewLayoutResourceIdProperty.GetValue(bindingActivity);

            if (viewLayoutResourceId > -1)
            {
                // Load the XML elements of the view
                xmlElements = GetViewAsXmlElements(bindingActivity, viewLayoutResourceId);
            }

            // If there is at least one 'Binding' attribute set in the XML file, get the view as objects
            if (xmlElements != null && xmlElements.Any(xe => xe.Attribute(BindingOperationXmlNamespace) != null))
            {
                viewElements = GetViewAsObjects(bindingActivity);
            }

            if (xmlElements != null && xmlElements.Any() && viewElements != null && viewElements.Any())
            {
                // Get all the binding operations inside the XML file.
                var bindingOperations = ExtractBindingOperationsFromLayoutFile(xmlElements, viewElements);
                if (bindingOperations != null && bindingOperations.Any())
                {
                    // Find the value of the DataContext property (which is, in fact, our ViewModel)
                    var viewModel = bindingActivity.DataContext as BindableObject;
                    if (viewModel != null)
                    {
                        // Load all the converters if there is a binding using a converter
                        if (bindingOperations.Any(bo => !string.IsNullOrWhiteSpace(bo.Converter)))
                        {
                            var valueConverters = GetAllValueConverters();
                            ValueConverters.AddRange(valueConverters.Where(valueConverter => !ValueConverters.Contains(valueConverter)));
                        }

                        var bindingRelationships = new List<BindingRelationship>();

                        // OneWay bindings: all changes to any properties of the ViewModel will need to update the dedicated properties on controls
                        viewModel.PropertyChanged += (sender, args) =>
                        {
                            if (_preventUpdateForTargetProperty)
                                return;

                            if (bindingRelationships.Any(p => p.SourceProperty.Name == args.PropertyName))
                            {
                                foreach (var bindingRelationship in bindingRelationships.Where(p => p.SourceProperty.Name == args.PropertyName))
                                {
                                    _preventUpdateForSourceProperty = true;

                                    // Get the value of the source (ViewModel) property by using the converter if needed
                                    var sourcePropertyValue = bindingRelationship.Converter == null ? bindingRelationship.SourceProperty.GetValue(viewModel) : bindingRelationship.Converter.Convert(bindingRelationship.SourceProperty.GetValue(viewModel), bindingRelationship.TargetProperty.PropertyType, bindingRelationship.ConverterParameter, CultureInfo.CurrentCulture);

                                    bindingRelationship.TargetProperty.SetValue(bindingRelationship.Control, sourcePropertyValue);

                                    _preventUpdateForSourceProperty = false;
                                }
                            }
                        };

                        // For each binding operations, bind from the source (ViewModel) to the target (Control) 
                        // and from the target (Control) to the source (ViewModel) in case of a TwoWay binding.
                        foreach (var bindingOperation in bindingOperations)
                        {
                            var sourceProperty = typeof(TViewModel).GetProperty(bindingOperation.Source);

                            var bindingEvent = bindingOperation.Control.GetType().GetEvent(bindingOperation.Target);
                            if (bindingEvent != null)
                            {
                                // The target is an event of the control

                                if (sourceProperty != null)
                                {
                                    // We need to ensure that the bound property implements the interface ICommand so we can call the "Execute" method
                                    var command = sourceProperty.GetValue(viewModel) as ICommand;
                                    if (command == null)
                                    {
                                        throw new InvalidOperationException(string.Format("The source property {0}, bound to the event {1}, needs to implement the interface ICommand.", bindingOperation.Source, bindingEvent.Name));
                                    }

                                    // Add an event handler to the specified event to execute the command when event is raised
                                    var executeMethodInfo = typeof(ICommand).GetMethod("Execute", new[] { typeof(object) });

                                    AddHandler(bindingOperation.Control, bindingOperation.Target, () =>
                                    {
                                        if (!_preventUpdateForSourceProperty)
                                        {
                                            executeMethodInfo.Invoke(command, new object[] { null });
                                        }
                                    });

                                    // Add an event handler to manage the CanExecuteChanged event of the command (so we can disable/enable the control attached to the command)
                                    var currentControl = bindingOperation.Control;

                                    var enabledProperty = currentControl.GetType().GetProperty("Enabled");
                                    if (enabledProperty != null)
                                    {
                                        enabledProperty.SetValue(currentControl, command.CanExecute(null));

                                        AddHandler(command, "CanExecuteChanged", () => enabledProperty.SetValue(currentControl, command.CanExecute(null)));
                                    }
                                }
                                else
                                {
                                    // If the Source property of the ViewModel is not a 'real' property, check if it's a method
                                    var sourceMethod = typeof(TViewModel).GetMethod(bindingOperation.Source, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                                    if (sourceMethod != null)
                                    {
                                        if (sourceMethod.GetParameters().Length > 0)
                                        {
                                            // We only support calls to methods without parameters
                                            throw new InvalidOperationException(string.Format("Method {0} should not have any parameters to be called when event {1} is raised.", sourceMethod.Name, bindingEvent.Name));
                                        }

                                        // If it's a method, add a event handler to the specified event to execute the method when event is raised
                                        AddHandler(bindingOperation.Control, bindingOperation.Target, () =>
                                        {
                                            if (!_preventUpdateForSourceProperty)
                                            {
                                                sourceMethod.Invoke(viewModel, null);
                                            }
                                        });
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException(string.Format("No property or event named {0} found to bint it to the event {1}.", bindingOperation.Source, bindingEvent.Name));
                                    }
                                }
                            }
                            else
                            {
                                if (sourceProperty == null)
                                {
                                    throw new InvalidOperationException(string.Format("Source property {0} does not exist on {1}.", bindingOperation.Source, typeof(TViewModel).Name));
                                }

                                // The target is a property of the control
                                var targetProperty = bindingOperation.Control.GetType().GetProperty(bindingOperation.Target);
                                if (targetProperty == null)
                                {
                                    throw new InvalidOperationException(string.Format("Target property {0} of the XML binding operation does not exist on {1}.", bindingOperation.Target, bindingOperation.Control.GetType().Name));
                                }

                                // If there is a Converter provided, instanciate it and use it to convert the value
                                var valueConverterName = bindingOperation.Converter;
                                IBindingValueConverter valueConverter = null;

                                if (!string.IsNullOrWhiteSpace(valueConverterName))
                                {
                                    var valueConverterType = ValueConverters.FirstOrDefault(t => t.Name == valueConverterName);
                                    if (valueConverterType != null)
                                    {
                                        var valueConverterCtor = valueConverterType.GetConstructor(Type.EmptyTypes);
                                        if (valueConverterCtor != null)
                                        {
                                            valueConverter = valueConverterCtor.Invoke(null) as IBindingValueConverter;
                                        }
                                        else
                                        {
                                            throw new InvalidOperationException(string.Format("Value converter {0} need an empty constructor to be instanciated.", valueConverterName));
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException(string.Format("There is no converter named {0}.", valueConverterName));
                                    }
                                }

                                var valueConverterParameter = bindingOperation.ConverterParameter;

                                // Get the value of the source (ViewModel) property by using the converter if needed
                                var sourcePropertyValue = valueConverter == null ? sourceProperty.GetValue(viewModel) : valueConverter.Convert(sourceProperty.GetValue(viewModel), targetProperty.PropertyType, valueConverterParameter, CultureInfo.CurrentCulture);

                                // Set initial binding value
                                targetProperty.SetValue(bindingOperation.Control, sourcePropertyValue);

                                // Add a relationship between the source (ViewModel) and the target (Control) so we can update the target property when the source changed (OneWay binding)
                                bindingRelationships.Add(new BindingRelationship { SourceProperty = sourceProperty, TargetProperty = targetProperty, Converter = valueConverter, ConverterParameter = bindingOperation.ConverterParameter, Control = bindingOperation.Control });

                                if (bindingOperation.Mode == BindingMode.TwoWay)
                                {
                                    // TwoWay bindings: Update the ViewModel property when the dedicated event is raised on the bound control
                                    var controlType = bindingOperation.Control.GetType();

                                    // Bind controls' events to update the associated ViewModel property
                                    
                                    #region Bind controls' events to update the associated ViewModel property

                                    // TODO: Need to improve this!
                                    if (controlType == typeof(CalendarView))
                                    {
                                        ((CalendarView)bindingOperation.Control).DateChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, new DateTime(args.Year, args.Month, args.DayOfMonth), valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(CheckBox))
                                    {
                                        ((CheckBox)bindingOperation.Control).CheckedChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.IsChecked, valueConverter, valueConverterParameter);
                                    }
                                    if (controlType == typeof(EditText))
                                    {
                                        ((EditText)bindingOperation.Control).TextChanged += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.Text.ToString(), valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(RadioButton))
                                    {
                                        ((RadioButton)bindingOperation.Control).CheckedChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.IsChecked, valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(RatingBar))
                                    {
                                        ((RatingBar)bindingOperation.Control).RatingBarChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.Rating, valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(SearchView))
                                    {
                                        ((SearchView)bindingOperation.Control).QueryTextChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.NewText, valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(Switch))
                                    {
                                        ((Switch)bindingOperation.Control).CheckedChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.IsChecked, valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(TimePicker))
                                    {
                                        ((TimePicker)bindingOperation.Control).TimeChanged += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, new TimeSpan(args.HourOfDay, args.Minute, 0), valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(ToggleButton))
                                    {
                                        ((ToggleButton)bindingOperation.Control).CheckedChange += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.IsChecked, valueConverter, valueConverterParameter);
                                    }
                                    else if (controlType == typeof(SeekBar))
                                    {
                                        ((SeekBar)bindingOperation.Control).ProgressChanged += (sender, args) => UpdateSourceProperty(sourceProperty, viewModel, args.Progress, valueConverter, valueConverterParameter);
                                    }

                                    #endregion
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns the current view (activity) as a list of XML element.
        /// </summary>
        /// <typeparam name="TViewModel">The type of the ViewModel associated to the activity.</typeparam>
        /// <param name="bindingActivity">The current activity we want to get as a list of XML elements.</param>
        /// <param name="viewLayoutResourceId">The id corresponding to the layout.</param>
        /// <returns>A list of XML elements which represent the XML layout of the view.</returns>
        private static List<XElement> GetViewAsXmlElements<TViewModel>(BindingActivity<TViewModel> bindingActivity, int viewLayoutResourceId) where TViewModel : BindableObject
        {
            List<XElement> xmlElements;

            using (var viewAsXmlReader = bindingActivity.Resources.GetLayout(viewLayoutResourceId))
            {
                using (var sb = new StringBuilder())
                {
                    while (viewAsXmlReader.Read())
                    {
                        sb.Append(viewAsXmlReader.ReadOuterXml());
                    }

                    var viewAsXDocument = XDocument.Parse(sb.ToString());
                    xmlElements = viewAsXDocument.Descendants().ToList();
                }
            }

            return xmlElements;
        }

        /// <summary>
        /// Returns the current view (activity) as a list of .NET objects.
        /// </summary>
        /// <typeparam name="TViewModel">The type of the ViewModel associated to the activity.</typeparam>
        /// <param name="bindingActivity">The current activity we want to get as a list of XML elements.</param>
        /// <returns>A list of .NET objects which composed the view.</returns>
        private static List<View> GetViewAsObjects<TViewModel>(BindingActivity<TViewModel> bindingActivity) where TViewModel : BindableObject
        {
            // Get the objects on the view
            var rootView = bindingActivity.Window.DecorView.FindViewById(Resource.Id.Content);

            return GetAllChildrenInView(rootView, true);
        }

        /// <summary>
        /// Recursive method which returns the list of children contains in a view.
        /// </summary>
        /// <param name="rootView">The root/start view from which the analysis is performed.</param>
        /// <param name="isTopRootView">True is the current root element is, in fact, the top view.</param>
        /// <returns>A list containing all the views with their childrens.</returns>
        private static List<View> GetAllChildrenInView(View rootView, bool isTopRootView = false)
        {
            if (!(rootView is ViewGroup))
            {
                return new List<View> { rootView };
            }

            var childrens = new List<View>();

            var viewGroup = (ViewGroup)rootView;

            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                var child = viewGroup.GetChildAt(i);

                var childList = new List<View>();
                if (isTopRootView)
                {
                    childList.Add(child);
                }

                childList.AddRange(GetAllChildrenInView(child));

                childrens.AddRange(childList);
            }

            return childrens;
        }

        /// <summary>
        /// Extract the Binding operations (represent by the Binding="" attribute in the XML file).
        /// </summary>
        /// <param name="xmlElements">The list of XML elements from which we want to extract the Binding operations.</param>
        /// <param name="viewElements">The list of .NET objects corresponding to the elements of the view.</param>
        /// <returns>A list containing all the binding operations (matching between the Source property, the Target property, the Control bound to the .NET property and the Mode of the binding).</returns>
        private static List<BindingOperation> ExtractBindingOperationsFromLayoutFile(List<XElement> xmlElements, List<View> viewElements)
        {
            var bindingOperations = new List<BindingOperation>();

            for (int i = 0; i < xmlElements.Count; i++)
            {
                var currentXmlElement = xmlElements.ElementAt(i);

                if (currentXmlElement.Attributes(BindingOperationXmlNamespace).Any())
                {
                    var xmlBindings = currentXmlElement.Attributes(BindingOperationXmlNamespace);

                    foreach (var xmlBindingAttribute in xmlBindings)
                    {

                        var xmlBindingValue = xmlBindingAttribute.Value;

                        if (!xmlBindingValue.StartsWith("{") || !xmlBindingValue.EndsWith("}"))
                        {
                            throw new InvalidOperationException(string.Format("The following XML binding operation is not well formatted, it should start with '{{' and end with '}}:'{0}{1}", Environment.NewLine, xmlBindingValue));
                        }

                        var xmlBindingOperations = xmlBindingValue.Split(';');

                        foreach (var bindingOperation in xmlBindingOperations)
                        {
                            if (!bindingOperation.Contains(","))
                            {
                                throw new InvalidOperationException(string.Format("The following XML binding operation is not well formatted, it should contains at least one ',' between Source and Target:{0}{1}", Environment.NewLine, xmlBindingValue));
                            }

                            // Source properties can be nested properties: MyObject.MyProperty.SampleProperty
                            var bindingSourceValueRegex = new Regex(@"Source=(\w+(.\w+)+)");
                            var bindingSourceValue = bindingSourceValueRegex.Match(bindingOperation).Groups[1].Value;

                            var bindingTargetValueRegex = new Regex(@"Target=(\w+)");
                            var bindingTargetValue = bindingTargetValueRegex.Match(bindingOperation).Groups[1].Value;

                            var bindingConverterValueRegex = new Regex(@"Converter=(\w+)");
                            var bindingConverterValue = bindingConverterValueRegex.Match(bindingOperation).Groups[1].Value;

                            // Converter parameter support using more than just a word.
                            var bindingConverterParameterValueRegex = new Regex(@"ConverterParameter='(\w+\s(.\w+)+)");
                            var bindingConverterParameterValue = bindingConverterParameterValueRegex.Match(bindingOperation).Groups[1].Value;

                            var bindingModeValue = BindingMode.OneWay;

                            var bindingModeValueRegex = new Regex(@"Mode=(\w+)");
                            var bindingModeValueRegexMatch = bindingModeValueRegex.Match(bindingOperation);

                            if (bindingModeValueRegexMatch.Success)
                            {
                                if (!System.Enum.TryParse(bindingModeValueRegexMatch.Groups[1].Value, true, out bindingModeValue))
                                {
                                    throw new InvalidOperationException(string.Format("The Mode property of the following XML binding operation is not well formatted, it should be 'OneWay' or 'TwoWay':{0}{1}", Environment.NewLine, xmlBindingValue));
                                }
                            }

                            bindingOperations.Add(new BindingOperation { Control = viewElements.ElementAt(i), Source = bindingSourceValue, Target = bindingTargetValue, Converter = bindingConverterValue, ConverterParameter = bindingConverterParameterValue, Mode = bindingModeValue });
                        }

                    }
                }
            }

            return bindingOperations;
        }

        /// <summary>
        /// Update a property from the target (.NET object) to the source (ViewModel). This method is used when in a TwoWay binding, to update the source property.
        /// </summary>
        /// <typeparam name="T">The type of the source property.</typeparam>
        /// <param name="sourceProperty">The source property to update.</param>
        /// <param name="viewModel">The ViewModel on which the property will be updated.</param>
        /// <param name="value">The new value to set the source property.</param>
        /// <param name="valueConverter">The converter to use to convert back the target (.NET object) property to the source (ViewModel) property.</param>
        /// <param name="converterParameter">The optional paramater to use in the value converter.</param>
        private static void UpdateSourceProperty<T>(PropertyInfo sourceProperty, BindableObject viewModel, T value, IBindingValueConverter valueConverter, string converterParameter)
        {
            _preventUpdateForTargetProperty = true;

            sourceProperty.SetValue(viewModel, valueConverter == null ? value : valueConverter.ConvertBack(value, sourceProperty.PropertyType, converterParameter, CultureInfo.CurrentCulture));

            _preventUpdateForTargetProperty = false;
        }

        /// <summary>
        /// Helper to dynamically add an event handler to a control.
        /// Source: http://stackoverflow.com/questions/5658765/create-a-catch-all-handler-for-all-events-and-delegates-in-c-sharp
        /// </summary>
        /// <param name="target">The control on which we want to add the event handler.</param>
        /// <param name="eventName">The name of the event on which we want to add a handler.</param>
        /// <param name="methodToExecute">The code we want to execute when the handler is raised.</param>
        private static void AddHandler(object target, string eventName, Action methodToExecute)
        {
            var eventInfo = target.GetType().GetEvent(eventName);
            if (eventInfo != null)
            {
                var delegateType = eventInfo.EventHandlerType;
                var dynamicHandler = BuildDynamicHandler(delegateType, methodToExecute);

                eventInfo.GetAddMethod().Invoke(target, new object[] { dynamicHandler });
            }
        }

        /// <summary>
        /// Build a delegate for a particular type.
        /// </summary>
        /// <param name="delegateType">The type of the object for which we want the delegate.</param>
        /// <param name="methodToExecute">The code we want to execute when the handler is raised.</param>
        /// <returns>A delegate object for the dedicated type, used the execute the specified code.</returns>
        private static Delegate BuildDynamicHandler(Type delegateType, Action methodToExecute)
        {
            var invokeMethod = delegateType.GetMethod("Invoke");
            var parms = invokeMethod.GetParameters().Select(parm => Expression.Parameter(parm.ParameterType, parm.Name)).ToArray();
            var instance = methodToExecute.Target == null ? null : Expression.Constant(methodToExecute.Target);
            var call = Expression.Call(instance, methodToExecute.Method);
            var body = invokeMethod.ReturnType == typeof(void) ? (Expression)call : Expression.Convert(call, invokeMethod.ReturnType);
            var expr = Expression.Lambda(delegateType, body, parms);

            return expr.Compile();
        }

        private static IEnumerable<Type> GetAllValueConverters()
        {
            var allCustomAssemblies = GetAllCustomAssemblies();

            foreach (var customAssembly in allCustomAssemblies)
            {
                var assembly = Assembly.Load(customAssembly);

                if (assembly.GetTypes().Any(t => typeof(IBindingValueConverter).IsAssignableFrom(t)))
                {
                    return assembly.GetTypes().Where(t => typeof(IBindingValueConverter).IsAssignableFrom(t));
                }
            }

            return null;
        }

        private static IEnumerable<AssemblyName> GetAllCustomAssemblies()
        {
            var stackFrames = new StackTrace().GetFrames();
            if (stackFrames == null)
            {
                return null;
            }

            var assemblyNames = new List<AssemblyName>();

            var executingAssembly = Assembly.GetExecutingAssembly();

            foreach (var frame in stackFrames)
            {
                var declaringType = frame.GetMethod().DeclaringType;
                if (declaringType != null)
                {
                    var assembly = declaringType.Assembly;
                    if (assembly.GetName().Name != executingAssembly.GetName().Name)
                    {
                        var currentAssemblyName = assembly.GetName();

                        if (!IsSystemAssembly(currentAssemblyName.Name) && !assemblyNames.Contains(currentAssemblyName))
                        {
                            assemblyNames.Add(currentAssemblyName);
                        }

                        var referencedAssemblies = assembly.GetReferencedAssemblies();
                        if (referencedAssemblies.Any())
                        {
                            foreach (var referencedAssembly in referencedAssemblies)
                            {
                                if (referencedAssembly.Name != executingAssembly.GetName().Name)
                                {
                                    if (!IsSystemAssembly(referencedAssembly.Name) && !assemblyNames.Contains(referencedAssembly))
                                    {
                                        assemblyNames.Add(referencedAssembly);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return assemblyNames;
        }

        private static bool IsSystemAssembly(string assemblyName)
        {
            return string.Equals(assemblyName, "Mono.Android", StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(assemblyName, "mscorlib", StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(assemblyName, "System", StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(assemblyName, "System.Core", StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(assemblyName, "System.Xml", StringComparison.InvariantCultureIgnoreCase) ||
                   string.Equals(assemblyName, "System.Xml.Linq", StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
