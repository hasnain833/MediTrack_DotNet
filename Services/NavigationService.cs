using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using DChemist.ViewModels;

namespace DChemist.Services
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
            System.Diagnostics.Debug.WriteLine($"[NavigationService] NavigateRoot requested: {pageTypeFullName}");
            if (_rootFrame == null) 
            {
                System.Diagnostics.Debug.WriteLine("[NavigationService] ERROR: _rootFrame is NULL");
                return false;
            }
            var type = ResolveType(pageTypeFullName);
            if (type == null) 
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] ERROR: Could not resolve type: {pageTypeFullName}");
                return false;
            }
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Successfully resolved type, proceeding with navigation.");
            return _rootFrame.Navigate(type, parameter);
        }

        public bool Navigate(string pageTypeFullName, object? parameter = null)
        {
            System.Diagnostics.Debug.WriteLine($"[NavigationService] Navigate requested: {pageTypeFullName}");
            if (_frame == null) 
            {
                System.Diagnostics.Debug.WriteLine("[NavigationService] ERROR: _frame is NULL");
                return false;
            }
            
            var type = ResolveType(pageTypeFullName);
            if (type == null) 
            {
                System.Diagnostics.Debug.WriteLine($"[NavigationService] ERROR: Could not resolve type: {pageTypeFullName}");
                return false;
            }
            
            // Simple guard check
            var auth = _services.GetService<AuthorizationService>();
            if (auth != null)
            {
                bool isAdminOnly = pageTypeFullName.Contains("FinancialPage") || pageTypeFullName.Contains("SettingsPage");
                if (isAdminOnly && !auth.IsAdmin)
                {
                    System.Diagnostics.Debug.WriteLine($"[NavigationService] BLOCKED: Admin only page.");
                    return false; 
                }
            }

            System.Diagnostics.Debug.WriteLine($"[NavigationService] Navigating frame to: {type.Name}");
            return _frame.Navigate(type, parameter);
        }

        private Type? ResolveType(string fullName)
        {
            var type = Type.GetType(fullName);
            if (type == null)
            {
                // Fallback for cases where Type.GetType needs assembly info
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    type = assembly.GetType(fullName);
                    if (type != null) break;
                }
            }
            return type;
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
