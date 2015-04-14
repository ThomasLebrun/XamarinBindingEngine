using System;
using System.Linq.Expressions;

namespace BindingEngine.Android.ViewModels
{
    public static class ViewModelFactory
    {
        public static TViewModel Create<TViewModel>()
        {
            var viewModelConstructor = InstanceCreatorOfViewModel<TViewModel>();

            return viewModelConstructor();
        }

        private static Func<TViewModel> InstanceCreatorOfViewModel<TViewModel>()
        {
            // Use a Compiled Expression to instanciate the ctor 
            // This is to improve the performances: http://vagif.bloggingabout.net/2010/04/02/dont-use-activator-createinstance-or-constructorinfo-invoke-use-compiled-lambda-expressions/
            
            var ctor = typeof(TViewModel).GetConstructor(Type.EmptyTypes);
            if (ctor == null)
                return null;

            return Expression.Lambda<Func<TViewModel>>(Expression.New(ctor)).Compile();
        }
    }
}