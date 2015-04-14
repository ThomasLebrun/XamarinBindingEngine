# Xamarin Binding Engine
This is a small proof of concept that shows how to implement a XML attributes based binding engine for Xamarin.Android applications.

You'll be able to find more details on my blog (http://blog.thomaslebrun.net/2015/03/xamarin-implement-your-own-binding-engine-based-on-xml-attributes-for-you-xamarin-android-applications/) but here are small examples of what can be achieved:
* Simple OneWay binding:
```xml
<EditText
	android:layout_width="fill_parent"
	android:layout_height="wrap_content"
	android:text="@string/Hello"
	local:Binding="{Source=SampleText, Target=Text}" />
```

* Simple TwoWay binding:
```xml
<EditText
	android:layout_width="fill_parent"
	android:layout_height="wrap_content"
	android:text="@string/Hello"
	local:Binding="{Source=SampleText, Target=Text, Mode=TwoWay}" />
```

* Binding an event to a command:
```xml
<Button
	android:id="@+id/MyButton"
	android:layout_width="fill_parent"
	android:layout_height="wrap_content"
	android:text="Change EditText From Command"
	local:Binding="{Source=ButtonClickCommand, Target=Click}" />
```

* Binding an event to a method:
```xml
<Button
	android:layout_width="fill_parent"
	android:layout_height="wrap_content"
	android:text="Change EditText From Method"
	local:Binding="{Source=ClickOnButton, Target=Click}" />
```

* Binding with a converter & converter parameter:
```xml
<EditText
	android:layout_width="fill_parent"
	android:layout_height="wrap_content"
	android:text="@string/Hello"
	local:Binding="{Source=SampleBool, Target=Text, Mode=TwoWay, Converter=BooleanToStringConverter, ConverterParameter='You can put any string here'}" />
```

Feel free to reach me on my blog (http://blog.thomaslebrun.net/), via Twitter (https://twitter.com/thomas_lebrun) or using the Issues section (https://github.com/ThomasLebrun/XamarinBindingEngine/issues).

Also, I'm accepting pull requests so don't hesitate to collaborate!