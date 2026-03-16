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
        private readonly SettingsService _settings;
        private string _pharmacyName = "";
        private string _pharmacyAddress = "";
        private string _pharmacyPhone = "";
        private string _pharmacyLicense = "";
        private string _pharmacyNtn = "";

        public SettingsViewModel(BackupService backupService, IDialogService dialogService, AuthorizationService auth, SettingsService settings)
        {
            _backupService = backupService;
            _dialogService = dialogService;
            _auth = auth;
            _settings = settings;

            BackupCommand = new AsyncRelayCommand(async _ => await _backupService.RunBackupAsync());
            RestoreCommand = new AsyncRelayCommand(async _ => await _backupService.RestoreDatabaseAsync());
            ShowFiscalSettingsCommand = new AsyncRelayCommand(async _ => await _dialogService.ShowFiscalSettingsDialogAsync());
            SavePharmacyDetailsCommand = new AsyncRelayCommand(ExecuteSavePharmacyDetailsAsync);
            
            _ = InitializeAsync();
        }

        public async Task InitializeAsync()
        {
            PharmacyName = await _settings.GetPharmacyNameAsync();
            PharmacyAddress = await _settings.GetPharmacyAddressAsync();
            PharmacyPhone = await _settings.GetPharmacyPhoneAsync();
            PharmacyLicense = await _settings.GetPharmacyLicenseAsync();
            PharmacyNtn = await _settings.GetPharmacyNtnAsync();
        }

        public string PharmacyName { get => _pharmacyName; set => SetProperty(ref _pharmacyName, value); }
        public string PharmacyAddress { get => _pharmacyAddress; set => SetProperty(ref _pharmacyAddress, value); }
        public string PharmacyPhone { get => _pharmacyPhone; set => SetProperty(ref _pharmacyPhone, value); }
        public string PharmacyLicense { get => _pharmacyLicense; set => SetProperty(ref _pharmacyLicense, value); }
        public string PharmacyNtn { get => _pharmacyNtn; set => SetProperty(ref _pharmacyNtn, value); }

        public ICommand SavePharmacyDetailsCommand { get; }

        private async Task ExecuteSavePharmacyDetailsAsync(object? parameter)
        {
            await _settings.SaveSettingAsync("pharmacy_name", PharmacyName);
            await _settings.SaveSettingAsync("pharmacy_address", PharmacyAddress);
            await _settings.SaveSettingAsync("pharmacy_phone", PharmacyPhone);
            await _settings.SaveSettingAsync("pharmacy_license", PharmacyLicense);
            await _settings.SaveSettingAsync("pharmacy_ntn", PharmacyNtn);
            await _dialogService.ShowMessageAsync("Success", "Pharmacy details updated successfully.");
        }

        public ICommand BackupCommand { get; }
        public ICommand RestoreCommand { get; }
        public ICommand ShowFiscalSettingsCommand { get; }
        public bool IsAdmin => _auth.IsAdmin;
    }
}
