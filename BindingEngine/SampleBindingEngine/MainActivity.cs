using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using BindingEngine.Android;
using BindingEngine.Android.Views;
using Uri = Android.Net.Uri;

namespace SampleBindingEngine
{
    [Activity(Label = "SampleBindingEngine", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : BindingActivity<MainViewModel>
    {
        int _count = 1;

        public MainActivity()
            : base(Resource.Layout.Main)
        {
        }

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);

            var button = FindViewById<Button>(Resource.Id.MyButton);

            //button.Click += delegate
            //{
            //    button.Text = this._count++.ToString(CultureInfo.InvariantCulture);

            //    this.DataContext.SampleText = "Does it work?";
            //    this.DataContext.SampleText2 = "Yes, it is!";
            //};

            this.FindViewById<Button>(Resource.Id.ChangeActivityButton).Click += (sender, args) =>
            {
                var intent = new Intent(Intent.ActionDial, Uri.Parse("tel:0123456789"));

                this.StartActivity(intent);
            };
        }
    }
}

