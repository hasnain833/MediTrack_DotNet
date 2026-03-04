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
        private readonly NavigationService _navigationService;
        private string _title = "Dashboard";
        private User? _currentUser;
        private NavigationItem? _selectedItem;

        public MainViewModel(AuthService authService, NavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
            CurrentUser = _authService.CurrentUser;

            NavigationItems = new ObservableCollection<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = "\uE80F", PageType = "MediTrack.Views.DashboardPage" },
                new NavigationItem { Title = "Inventory", Icon = "\uE811", PageType = "MediTrack.Views.InventoryPage" },
                new NavigationItem { Title = "Billing", Icon = "\uE825", PageType = "MediTrack.Views.BillingPage" },
                new NavigationItem { Title = "Financials", Icon = "\uE8C0", PageType = "MediTrack.Views.FinancialPage" },
                new NavigationItem { Title = "Users", Icon = "\uE716", PageType = "MediTrack.Views.UserManagementPage" }
            };

            LogoutCommand = new RelayCommand(_ => ExecuteLogout());
            
            SelectedItem = NavigationItems[0];
        }

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
            _navigationService.Navigate("MediTrack.Views.LoginPage");
        }
    }

    public class NavigationItem
    {
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string PageType { get; set; } = string.Empty;
    }
}
