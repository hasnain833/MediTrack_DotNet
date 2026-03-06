using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using MediTrack.Models;
using MediTrack.Services;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly AuthorizationService _auth;
        private readonly NavigationService _navigationService;
        private string _title = "Dashboard";
        private User? _currentUser;
        private NavigationItem? _selectedItem;

        public MainViewModel(AuthService authService, AuthorizationService auth, NavigationService navigationService)
        {
            _authService = authService;
            _auth = auth;
            _navigationService = navigationService;
            CurrentUser = _authService.CurrentUser;

            var items = new List<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = "\uE80F", PageType = "MediTrack.Views.DashboardPage", RequiresAdmin = false },
                new NavigationItem { Title = "Inventory", Icon = "\uE811", PageType = "MediTrack.Views.InventoryPage", RequiresAdmin = false },
                new NavigationItem { Title = "Billing", Icon = "\uE825", PageType = "MediTrack.Views.BillingPage", RequiresAdmin = false },
                new NavigationItem { Title = "Financials", Icon = "\uE8C0", PageType = "MediTrack.Views.FinancialPage", RequiresAdmin = true }
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
            _navigationService.NavigateRoot("MediTrack.Views.LoginPage");
        }
    }

}
