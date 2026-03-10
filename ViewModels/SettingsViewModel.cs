using System.Windows.Input;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly BackupService _backupService;
        private readonly IDialogService _dialogService;
        private readonly AuthorizationService _auth;

        public SettingsViewModel(BackupService backupService, IDialogService dialogService, AuthorizationService auth)
        {
            _backupService = backupService;
            _dialogService = dialogService;
            _auth = auth;

            BackupCommand = new AsyncRelayCommand(async _ => await _backupService.RunBackupAsync());
            RestoreCommand = new AsyncRelayCommand(async _ => await _backupService.RestoreDatabaseAsync());
            ShowFiscalSettingsCommand = new AsyncRelayCommand(async _ => await _dialogService.ShowFiscalSettingsDialogAsync());
        }

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ShowFiscalSettingsCommand { get; }
        public bool IsAdmin => _auth.IsAdmin;
    }
}
