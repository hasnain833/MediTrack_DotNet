using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace DChemist.ViewModels
{
    public class InventoryViewModel : ViewModelBase, IDisposable
    {
        private readonly MedicineRepository _medicineRepository;
        private readonly AuthorizationService _auth;
        private readonly InventoryEventBus _eventBus;
        private readonly IReportingService _reportingService;
        private readonly IDialogService _dialogService;
        private readonly DispatcherQueue _dispatcher;
        private string _searchText = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _isBusy;

        public InventoryViewModel(MedicineRepository medicineRepository, AuthorizationService auth, InventoryEventBus eventBus, IReportingService reportingService, IDialogService dialogService)
        {
            _medicineRepository = medicineRepository;
            _auth = auth;
            _eventBus = eventBus;
            _reportingService = reportingService;
            _dialogService = dialogService;
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            Medicines = new ObservableCollection<Medicine>();
            RefreshCommand = new AsyncRelayCommand(async _ => await RefreshAsync());
            AddMedicineCommand = new AsyncRelayCommand(async _ => await ExecuteAddMedicineAsync());
            EditMedicineCommand = new AsyncRelayCommand(async m => await ExecuteEditMedicineAsync(m as Medicine));
            DeleteMedicineCommand = new AsyncRelayCommand(async m => await ExecuteDeleteMedicineAsync(m as Medicine));
            DeleteSelectedCommand = new AsyncRelayCommand(async _ => await ExecuteDeleteSelectedAsync());
            TogglePurchasePriceCommand = new RelayCommand(m => ExecuteTogglePurchasePrice(m as Medicine));
            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportInventoryToCsvAsync(Medicines));

            _eventBus.InventoryChanged += OnInventoryChanged;
        }
        public Task LoadAsync() => RefreshAsync();

        public ObservableCollection<Medicine> Medicines { get; }
        public bool IsAdmin => _auth.IsAdmin;

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = SearchAsync(); }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

        public ICommand RefreshCommand { get; }
        public ICommand AddMedicineCommand { get; }
        public ICommand EditMedicineCommand { get; }
        public ICommand DeleteMedicineCommand { get; }
        public ICommand DeleteSelectedCommand { get; }
        public ICommand TogglePurchasePriceCommand { get; }
        public ICommand ExportCommand { get; }

        private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                if (!string.IsNullOrWhiteSpace(_searchText))
                    await SearchAsync();
                else
                    await RefreshAsync();
            });
        }

        private void ExecuteTogglePurchasePrice(Medicine? medicine)
        {
            if (medicine != null)
                medicine.IsPurchasePriceVisible = !medicine.IsPurchasePriceVisible;
        }

        private async Task RefreshAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var list = await _medicineRepository.GetAllAsync();
                Medicines.Clear();
                foreach (var item in list) Medicines.Add(item);
            }
            catch (DataAccessException ex)
            {
                ErrorMessage = ex.Message;
                AppLogger.LogWarning($"InventoryViewModel.RefreshAsync: {ex.Message}");
            }
            catch (Exception ex)
            {
                ErrorMessage = "Unexpected error loading inventory.";
                AppLogger.LogError("InventoryViewModel.RefreshAsync unexpected error", ex);
            }
            finally { IsBusy = false; }
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) { await RefreshAsync(); return; }

            IsBusy = true;
            ErrorMessage = string.Empty;
            try
            {
                var list = await _medicineRepository.SearchAsync(SearchText);
                Medicines.Clear();
                foreach (var item in list) Medicines.Add(item);
            }
            catch (DataAccessException ex)
            {
                ErrorMessage = ex.Message;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Search failed unexpectedly.";
                AppLogger.LogError("InventoryViewModel.SearchAsync unexpected error", ex);
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteAddMedicineAsync()
        {
            var result = await _dialogService.ShowInventoryDialogAsync();
            if (result != null)
            {
                ErrorMessage = string.Empty;
                try
                {
                    await _medicineRepository.AddAsync(result);
                }
                catch (DataAccessException ex)
                {
                    ErrorMessage = ex.Message;
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Could not add medicine. Please try again.";
                    AppLogger.LogError("InventoryViewModel.ExecuteAddMedicineAsync unexpected error", ex);
                }
            }
        }

        private async Task ExecuteEditMedicineAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            
            var result = await _dialogService.ShowInventoryDialogAsync(medicine);
            if (result != null)
            {
                ErrorMessage = string.Empty;
                try
                {
                    await _medicineRepository.UpdateAsync(result);
                }
                catch (DataAccessException ex)
                {
                    ErrorMessage = ex.Message;
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Could not update medicine. Please try again.";
                    AppLogger.LogError("InventoryViewModel.ExecuteEditMedicineAsync unexpected error", ex);
                }
            }
        }

        private async Task ExecuteDeleteMedicineAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            
            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Medicine",
                $"Are you sure you want to delete {medicine.Name}?",
                "Delete",
                "Cancel"
            );
            
            if (confirmed)
            {
                ErrorMessage = string.Empty;
                try
                {
                    await _medicineRepository.DeleteAsync(medicine.Id);
                }
                catch (DataAccessException ex)
                {
                    ErrorMessage = ex.Message;
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Could not delete medicine. Please try again.";
                    AppLogger.LogError("InventoryViewModel.ExecuteDeleteMedicineAsync unexpected error", ex);
                }
            }
        }

        private async Task ExecuteDeleteSelectedAsync()
        {
            var selectedIds = Medicines.Where(m => m.IsSelected).Select(m => m.Id).ToList();
            if (!selectedIds.Any()) return;

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Selected Medicines",
                $"Are you sure you want to delete {selectedIds.Count} selected medicine(s)? This action cannot be undone.",
                "Delete All",
                "Cancel"
            );

            if (confirmed)
            {
                ErrorMessage = string.Empty;
                IsBusy = true;
                try
                {
                    await _medicineRepository.DeleteBulkAsync(selectedIds);
                    // The event bus will trigger a RefreshAsync, clearing the selected items.
                }
                catch (DataAccessException ex)
                {
                    // Postgres error 23503 is Foreign Key Violation
                    if (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == "23503")
                    {
                        ErrorMessage = "Cannot delete medicines that have already been sold. Check sales history.";
                    }
                    else
                    {
                        ErrorMessage = ex.Message;
                    }
                }
                catch (Exception ex)
                {
                    ErrorMessage = "Could not delete selected medicines. Please try again.";
                    AppLogger.LogError("InventoryViewModel.ExecuteDeleteSelectedAsync unexpected error", ex);
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        public void Dispose()
        {
            _eventBus.InventoryChanged -= OnInventoryChanged;
        }
    }
}
