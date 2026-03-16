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
        private readonly IDialogService _dialogService;
        private readonly AlertService _alertService;
        private readonly BackupService _backupService;
        private string _title = "Dashboard";
        private User? _currentUser;
        private NavigationItem? _selectedItem;

        public MainViewModel(AuthService authService, AuthorizationService auth, NavigationService navigationService, UpdateService updateService, IDialogService dialogService, AlertService alertService, BackupService backupService)
        {
            _authService = authService;
            _auth = auth;
            _navigationService = navigationService;
            _updateService = updateService;
            _dialogService = dialogService;
            _alertService = alertService;
            _backupService = backupService;
            CurrentUser = _authService.CurrentUser;

            var items = new List<NavigationItem>
            {
                new NavigationItem { Title = "Dashboard", Icon = "\uE80F", PageType = "DChemist.Views.DashboardPage", RequiresAdmin = false },
                new NavigationItem { Title = "Inventory", Icon = "\uE950", PageType = "DChemist.Views.InventoryPage", RequiresAdmin = false },
                new NavigationItem { Title = "Stock In",  Icon = "\uE896", PageType = "DChemist.Views.StockInPage",   RequiresAdmin = true  },
                new NavigationItem { Title = "Billing",   Icon = "\uE8A1", PageType = "DChemist.Views.BillingPage",   RequiresAdmin = false },
                new NavigationItem { Title = "Invoices",  Icon = "\uE990", PageType = "DChemist.Views.FinancialPage", RequiresAdmin = false },
                new NavigationItem { Title = "Daily Report", Icon = "\uE9D9", PageType = "DChemist.Views.FinancialReportPage", RequiresAdmin = true },
                new NavigationItem { Title = "Stock Adjustment", Icon = "\uE7BE", PageType = "DChemist.Views.InventoryAdjustmentPage", RequiresAdmin = true },
                new NavigationItem { Title = "Audit Logs", Icon = "\uE81C", PageType = "DChemist.Views.AuditLogsPage", RequiresAdmin = true },
                new NavigationItem { Title = "Settings", Icon = "\uE713", PageType = "DChemist.Views.SettingsPage", RequiresAdmin = true }
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
            ToggleSidebarCommand = new RelayCommand(_ => IsSidebarCollapsed = !IsSidebarCollapsed);
            
            // Check for updates on background thread
            _ = CheckForUpdatesAsync();

            // Check for alerts (dispatch to UI thread so dialogs don't crash)
            _ = CheckForAlertsAsync();

            // Run automated backup check
            _ = _backupService.CheckAndRunScheduledBackupAsync();
        }

        private bool _isSidebarCollapsed;
        public bool IsSidebarCollapsed
        {
            get => _isSidebarCollapsed;
            set
            {
                if (SetProperty(ref _isSidebarCollapsed, value))
                {
                    OnPropertyChanged(nameof(SidebarWidth));
                    OnPropertyChanged(nameof(SidebarGridWidth));
                    OnPropertyChanged(nameof(IsSidebarExpanded));
                }
            }
        }

        public double SidebarWidth => IsSidebarCollapsed ? 60 : 200;
        public Microsoft.UI.Xaml.GridLength SidebarGridWidth => new Microsoft.UI.Xaml.GridLength(SidebarWidth);
        public bool IsSidebarExpanded => !IsSidebarCollapsed;

        private async System.Threading.Tasks.Task CheckForAlertsAsync()
        {
            Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread().TryEnqueue(async () =>
            {
                await _alertService.CheckAndShowAlertsAsync();
            });
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
                        await _dialogService.ShowUpdateDialogAsync(update, _updateService);
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
        public ICommand ToggleSidebarCommand { get; }

        private async void ExecuteLogout()
        {
            _alertService.ResetSession();
            await _authService.LogoutAsync();
            _navigationService.NavigateRoot("DChemist.Views.LoginPage");
        }
    }

}
