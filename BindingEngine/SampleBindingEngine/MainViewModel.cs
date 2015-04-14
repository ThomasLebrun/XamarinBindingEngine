using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using BindingEngine.Android;
using BindingEngine.Android.Commands;
using SampleBindingEngine.Core;

namespace SampleBindingEngine
{
    public class MainViewModel : BindableObject
    {
        private bool _isBusy;

        public bool IsBusy
        {
            get { return _isBusy; }
            set
            {
                _isBusy = value;

                this.OnPropertyChanged();
            }
        }

        private string _sampleText;
        public string SampleText
        {
            get { return _sampleText; }
            set
            {
                _sampleText = value;

                this.OnPropertyChanged();

                if (this.ButtonClickCommand != null)
                {
                    this.ButtonClickCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private string _sampleText2;
        public string SampleText2
        {
            get { return _sampleText2; }
            set
            {
                _sampleText2 = value;

                this.OnPropertyChanged();

                if (this.TextCommand != null)
                {
                    this.TextCommand.RaiseCanExecuteChanged();
                }
            }
        }

        private bool _sampleBool;
        public bool SampleBool
        {
            get { return _sampleBool; }
            set
            {
                _sampleBool = value;

                this.OnPropertyChanged();
            }
        }

        private Person _person;
        public Person Person
        {
            get { return _person; }
            set
            {
                _person = value;

                this.OnPropertyChanged();
            }
        }

        public RelayCommand ButtonClickCommand { get; set; }

        public RelayCommand TextCommand { get; set; }

        public RelayCommand ChangePersonCommand { get; set; }

        public MainViewModel()
        {
            this.SampleText = "Hey, Xamarin World!";
            this.SampleText2 = "Hey, Xamarin World!";

            this.ButtonClickCommand = new RelayCommand(o =>
            {
                this.SampleText = "Text Changed from First Command!";
            },
            o => !string.IsNullOrWhiteSpace(this.SampleText));

            this.TextCommand = new RelayCommand(o =>
            {
                this.SampleText2 = "Text Changed from Second Command!";
            },
            o => !string.IsNullOrWhiteSpace(this.SampleText2));

            this.Person = new Person {FirstName = "Thomas", LastName = "LEBRUN"};

            this.ChangePersonCommand = new RelayCommand(o => this.Person = new Person { FirstName = "Bill", LastName = "GATES" });
        }

        private void ClickOnButton()
        {
            this.SampleText = "Text Changed from Method!";
        }
    }
}