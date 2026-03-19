using System.Windows.Input;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Services;
using DChemist.Utils;
using DChemist.Repositories;

namespace DChemist.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly BackupService      _backupService;
        private readonly IDialogService     _dialogService;
        private readonly AuthorizationService _auth;
        private readonly SettingsService    _settings;
        private readonly UpdateService      _updateService;
        private readonly SaleRepository      _saleRepo;

        private string _printerName         = "";
        private bool   _isSilentPrintEnabled;
        private List<string> _availablePrinters = new();
        private string _pharmacyName        = "";
        private string _pharmacyAddress     = "";
        private string _pharmacyPhone       = "";
        private string _pharmacyLicense     = "";
        private string _pharmacyNtn         = "";
        private string _updateStatusMessage = "";
        private bool   _isCheckingUpdates;

        public SettingsViewModel(
            BackupService backupService,
            IDialogService dialogService,
            AuthorizationService auth,
            SettingsService settings,
            UpdateService updateService,
            SaleRepository saleRepo)
        {
            _backupService  = backupService;
            _dialogService  = dialogService;
            _auth           = auth;
            _settings       = settings;
            _updateService  = updateService;
            _saleRepo       = saleRepo;

            BackupCommand              = new AsyncRelayCommand(async _ => await _backupService.RunBackupAsync());
            RestoreCommand             = new AsyncRelayCommand(async _ => await _backupService.RestoreDatabaseAsync());
            ShowFiscalSettingsCommand  = new AsyncRelayCommand(async _ => await _dialogService.ShowFiscalSettingsDialogAsync());
            SavePharmacyDetailsCommand = new AsyncRelayCommand(ExecuteSavePharmacyDetailsAsync);
            SavePrintingSettingsCommand = new AsyncRelayCommand(ExecuteSavePrintingSettingsAsync);
            CheckForUpdatesCommand     = new AsyncRelayCommand(async _ => await ExecuteCheckForUpdatesAsync());
            ResetSalesDataCommand      = new AsyncRelayCommand(ExecuteResetSalesDataAsync);

            _ = InitializeAsync();
        }

        // ── Properties ───────────────────────────────────────────────────────
        public string CurrentVersion => _updateService.CurrentVersion;

        public string UpdateStatusMessage
        {
            get => _updateStatusMessage;
            private set => SetProperty(ref _updateStatusMessage, value);
        }

        public bool IsCheckingUpdates
        {
            get => _isCheckingUpdates;
            private set => SetProperty(ref _isCheckingUpdates, value);
        }

        public string PharmacyName    { get => _pharmacyName;    set => SetProperty(ref _pharmacyName, value);    }
        public string PharmacyAddress { get => _pharmacyAddress; set => SetProperty(ref _pharmacyAddress, value); }
        public string PharmacyPhone   { get => _pharmacyPhone;   set => SetProperty(ref _pharmacyPhone, value);   }
        public string PharmacyLicense { get => _pharmacyLicense; set => SetProperty(ref _pharmacyLicense, value); }
        public string PharmacyNtn     { get => _pharmacyNtn;     set => SetProperty(ref _pharmacyNtn, value);     }

        public string PrinterName         { get => _printerName;          set => SetProperty(ref _printerName, value);          }
        public bool   IsSilentPrintEnabled { get => _isSilentPrintEnabled; set => SetProperty(ref _isSilentPrintEnabled, value); }
        public List<string> AvailablePrinters { get => _availablePrinters; set => SetProperty(ref _availablePrinters, value); }

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand SavePharmacyDetailsCommand  { get; }
        public ICommand SavePrintingSettingsCommand { get; }
        public ICommand BackupCommand               { get; }
        public ICommand RestoreCommand              { get; }
        public ICommand ShowFiscalSettingsCommand   { get; }
        public ICommand CheckForUpdatesCommand      { get; }
        public ICommand ResetSalesDataCommand       { get; }
        public bool IsAdmin => _auth.IsAdmin;

        // ── Init ─────────────────────────────────────────────────────────────
        public async Task InitializeAsync()
        {
            PharmacyName    = await _settings.GetPharmacyNameAsync();
            PharmacyAddress = await _settings.GetPharmacyAddressAsync();
            PharmacyPhone   = await _settings.GetPharmacyPhoneAsync();
            PharmacyLicense = await _settings.GetPharmacyLicenseAsync();
            PharmacyNtn     = await _settings.GetPharmacyNtnAsync();

            PrinterName          = await _settings.GetPrinterNameAsync();
            IsSilentPrintEnabled = await _settings.IsSilentPrintEnabledAsync();

            try
            {
                AvailablePrinters = System.Drawing.Printing.PrinterSettings
                    .InstalledPrinters.Cast<string>().ToList();
            }
            catch { /* Ignore if printer fetching fails */ }
        }

        // ── Update check ─────────────────────────────────────────────────────
        private async Task ExecuteCheckForUpdatesAsync()
        {
            if (IsCheckingUpdates) return;
            IsCheckingUpdates    = true;
            UpdateStatusMessage  = "Checking for updates…";

            try
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null)
                {
                    UpdateStatusMessage = $"Update available: v{update.LatestVersion}";
                    await _dialogService.ShowUpdateDialogAsync(update, _updateService);
                }
                else
                {
                    UpdateStatusMessage = $"✔ You're up to date (v{CurrentVersion})";
                }
            }
            catch (System.Exception ex)
            {
                AppLogger.LogError("SettingsViewModel.CheckForUpdates", ex);
                UpdateStatusMessage = "Could not check for updates. Please check your internet connection.";
            }
            finally
            {
                IsCheckingUpdates = false;
            }
        }

        // ── Save handlers ─────────────────────────────────────────────────────
        private async Task ExecuteSavePharmacyDetailsAsync(object? _)
        {
            await _settings.SaveSettingAsync("pharmacy_name",    PharmacyName);
            await _settings.SaveSettingAsync("pharmacy_address", PharmacyAddress);
            await _settings.SaveSettingAsync("pharmacy_phone",   PharmacyPhone);
            await _settings.SaveSettingAsync("pharmacy_license", PharmacyLicense);
            await _settings.SaveSettingAsync("pharmacy_ntn",     PharmacyNtn);
            await _dialogService.ShowMessageAsync("Success", "Pharmacy details updated successfully.");
        }

        private async Task ExecuteSavePrintingSettingsAsync(object? _)
        {
            await _settings.SaveSettingAsync("printer_name",          PrinterName);
            await _settings.SaveSettingAsync("silent_print_enabled",  IsSilentPrintEnabled.ToString().ToLower());
            await _dialogService.ShowMessageAsync("Success", "Printing configuration updated successfully.");
        }

        private async Task ExecuteResetSalesDataAsync(object? _)
        {
            if (!IsAdmin) return;

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Reset Sales Data", 
                "Are you sure you want to permanently delete ALL sales data? This cannot be undone and will unblock medicine deletions.",
                "Reset All Data",
                "Cancel");

            if (!confirm) return;

            try
            {
                await _saleRepo.PurgeSalesDataAsync();
                await _dialogService.ShowMessageAsync("Success", "All sales data has been cleared.");
            }
            catch (System.Exception ex)
            {
                AppLogger.LogError("SettingsViewModel.ExecuteResetSalesData", ex);
                await _dialogService.ShowMessageAsync("Error", "Failed to clear sales data: " + ex.Message);
            }
        }
    }
}
