using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class AuditLogsViewModel : ViewModelBase
    {
        private readonly AuditRepository _auditRepo;
        private readonly UserRepository _userRepo;
        private User? _selectedUser;
        private string? _selectedAction;
        private DateTimeOffset? _selectedDate;
        private bool _isBusy;

        public AuditLogsViewModel(AuditRepository auditRepo, UserRepository userRepo)
        {
            _auditRepo = auditRepo;
            _userRepo = userRepo;

            Logs = new ObservableCollection<AuditLog>();
            Users = new ObservableCollection<User>();
            Actions = new List<string> { "Login", "Sale Created", "Sale Voided", "Item Returned", "Stock Adjustment", "Price Updated", "Medicine Deleted" };

            RefreshCommand = new AsyncRelayCommand(LoadLogsAsync);
            ClearFiltersCommand = new RelayCommand(_ => ExecuteClearFilters());
            
            _ = LoadInitialDataAsync();
        }

        public ObservableCollection<AuditLog> Logs { get; }
        public ObservableCollection<User> Users { get; }
        public List<string> Actions { get; }

        public User? SelectedUser
        {
            get => _selectedUser;
            set { if (SetProperty(ref _selectedUser, value)) _ = LoadLogsAsync(); }
        }

        public string? SelectedAction
        {
            get => _selectedAction;
            set { if (SetProperty(ref _selectedAction, value)) _ = LoadLogsAsync(); }
        }

        public DateTimeOffset? SelectedDate
        {
            get => _selectedDate;
            set { if (SetProperty(ref _selectedDate, value)) _ = LoadLogsAsync(); }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RefreshCommand { get; }
        public ICommand ClearFiltersCommand { get; }

        private async Task LoadInitialDataAsync()
        {
            IsBusy = true;
            try
            {
                var users = await _userRepo.GetAllUsersAsync();
                Users.Clear();
                foreach (var u in users) Users.Add(u);
                
                await LoadLogsAsync();
            }
            finally { IsBusy = false; }
        }

        private async Task LoadLogsAsync()
        {
            IsBusy = true;
            try
            {
                var logs = await _auditRepo.GetLogsAsync(SelectedUser?.Id, SelectedAction, SelectedDate?.DateTime);
                Logs.Clear();
                foreach (var l in logs) Logs.Add(l);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load audit logs", ex);
            }
            finally { IsBusy = false; }
        }

        private void ExecuteClearFilters()
        {
            _selectedUser = null;
            OnPropertyChanged(nameof(SelectedUser));
            _selectedAction = null;
            OnPropertyChanged(nameof(SelectedAction));
            _selectedDate = null;
            OnPropertyChanged(nameof(SelectedDate));
            _ = LoadLogsAsync();
        }
    }
}
