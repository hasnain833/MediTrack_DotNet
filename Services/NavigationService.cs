using System;
using Microsoft.UI.Xaml.Controls;

namespace MediTrack.Services
{
    public class NavigationService
    {
        private Frame? _frame;

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public bool Navigate(string pageTypeFullName, object? parameter = null)
        {
            if (_frame == null) return false;
            
            var type = Type.GetType(pageTypeFullName);
            if (type == null) return false;

            return _frame.Navigate(type, parameter);
        }

        public bool Navigate(Type pageType, object? parameter = null)
        {
            if (_frame == null) return false;
            return _frame.Navigate(pageType, parameter);
        }

        public void GoBack()
        {
            if (_frame != null && _frame.CanGoBack)
            {
                _frame.GoBack();
            }
        }
    }
}
