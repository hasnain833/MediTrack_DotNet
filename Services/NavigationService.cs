using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Services
{
    public class NavigationService
    {
        private readonly IServiceProvider _services;
        private Frame? _frame;
        private Frame? _rootFrame;

        public NavigationService(IServiceProvider services)
        {
            _services = services;
        }

        public void Initialize(Frame frame)
        {
            _frame = frame;
        }

        public void InitializeRoot(Frame rootFrame)
        {
            _rootFrame = rootFrame;
        }

        public bool NavigateRoot(string pageTypeFullName, object? parameter = null)
        {
            if (_rootFrame == null) return false;
            var type = Type.GetType(pageTypeFullName);
            if (type == null) return false;
            return _rootFrame.Navigate(type, parameter);
        }

        public bool Navigate(string pageTypeFullName, object? parameter = null)
        {
            if (_frame == null) return false;
            
            var type = Type.GetType(pageTypeFullName);
            if (type == null) return false;

            // Simple guard check
            var auth = _services.GetService<AuthorizationService>();
            if (auth != null)
            {
                // Dashboards, Inventory, Billing are accessible to both
                // Financials and Users are Admin only
                bool isAdminOnly = pageTypeFullName.Contains("FinancialPage") || 
                                   pageTypeFullName.Contains("UserManagementPage");
                
                if (isAdminOnly && !auth.IsAdmin)
                {
                    return false; // Block navigation
                }
            }

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
