using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace BindingEngine.Android.Commands
{
    public class RelayCommand : ICommand
    {
        private readonly Func<object, bool> _canExecute;
        private readonly Action<object> _executeAction;

        public event EventHandler CanExecuteChanged;

        public RelayCommand(Action<object> executeAction)
            : this(executeAction, null)
        {
        }

        public RelayCommand(Action<object> executeAction, Func<object, bool> canExecute)
        {
            if (executeAction == null)
            {
                throw new ArgumentNullException("executeAction");
            }
            this._executeAction = executeAction;
            this._canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            var result = true;
            
            var canExecuteHandler = this._canExecute;
            if (canExecuteHandler != null)
            {
                result = canExecuteHandler(parameter);
            }

            return result;
        }

        public void RaiseCanExecuteChanged()
        {
            var handler = this.CanExecuteChanged;
            if (handler != null)
            {
                handler(this, new EventArgs());
            }
        }

        public void Execute(object parameter)
        {
            this._executeAction(parameter);
        }
    }
}