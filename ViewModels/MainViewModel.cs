using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly AuthorizationService _auth;
        private readonly NavigationService _navigationService;
        private readonly UpdateService _updateService;
        private string _title = "Dashboard";
        private User? _currentUser;
        private NavigationItem? _selectedItem;

        public MainViewModel(AuthService authService, AuthorizationService auth, NavigationService navigationService, UpdateService updateService)
        {
            _authService = authService;
            _auth = auth;
            _navigationService = navigationService;
            _updateService = updateService;
            CurrentUser = _authService.CurrentUser;

            var items = new List<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = "\uE80F", PageType = "DChemist.Views.DashboardPage", RequiresAdmin = false },
                new NavigationItem { Title = "Inventory", Icon = "\uE811", PageType = "DChemist.Views.InventoryPage", RequiresAdmin = false },
                new NavigationItem { Title = "Billing", Icon = "\uE825", PageType = "DChemist.Views.BillingPage", RequiresAdmin = false },
                new NavigationItem { Title = "Financials", Icon = "\uE8C0", PageType = "DChemist.Views.FinancialPage", RequiresAdmin = true }
            };

            NavigationItems = new ObservableCollection<NavigationItem>();
            foreach (var item in items)
            {
                if (!item.RequiresAdmin || _auth.IsAdmin)
                {
                    NavigationItems.Add(item);
                }
            }

            LogoutCommand = new RelayCommand(_ => ExecuteLogout());
            
            // Check for updates on background thread to not block UI
            _ = CheckForUpdatesAsync();
        }

        private async System.Threading.Tasks.Task CheckForUpdatesAsync()
        {
            try
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    // Use Dispatcher to show dialog from UI thread
                    Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
                    {
                        var dialog = new Views.UpdateDialog(update, _updateService);
                        dialog.XamlRoot = App.MainRoot?.XamlRoot;
                        await dialog.ShowAsync();
                    });
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.LogError("Update check error", ex);
            }
        }

        public bool IsAdmin => _auth.IsAdmin;

        public string Title { get => _title; set => SetProperty(ref _title, value); }
        public User? CurrentUser { get => _currentUser; set => SetProperty(ref _currentUser, value); }
        
        public NavigationItem? SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null)
                {
                    Title = value.Title;
                    _navigationService.Navigate(value.PageType);
                }
            }
        }

        public ObservableCollection<NavigationItem> NavigationItems { get; }
        public ICommand LogoutCommand { get; }

        private void ExecuteLogout()
        {
            _authService.Logout();
            _navigationService.NavigateRoot("DChemist.Views.LoginPage");
        }
    }

}
