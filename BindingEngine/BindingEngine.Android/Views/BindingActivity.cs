using System;
using System.Linq.Expressions;
using Android.App;
using Android.OS;
using BindingEngine.Android.ViewModels;

namespace BindingEngine.Android.Views
{
    public abstract class BindingActivity<TViewModel> : Activity where TViewModel : BindableObject
    {
        public TViewModel DataContext { get; set; }

        public int ViewLayoutResourceId { get; set; }

        protected BindingActivity(int viewLayoutResourceId)
        {
            this.ViewLayoutResourceId = viewLayoutResourceId;

            this.DataContext = ViewModelFactory.Create<TViewModel>();
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            this.SetContentView(this.ViewLayoutResourceId);

            BindingEngine.Initialize(this);
        }
    }
}