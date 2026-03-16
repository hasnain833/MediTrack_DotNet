using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class InventoryAdjustmentViewModel : ViewModelBase
    {
        private readonly MedicineRepository _medicineRepo;
        private readonly BatchRepository _batchRepo;
        private readonly AuditRepository _auditRepo;
        private readonly AuthService _authService;
        private readonly AuthorizationService _authorizationService;

        private string _searchText = string.Empty;
        private Medicine? _selectedMedicine;
        private InventoryBatch? _selectedBatch;
        private int _newQuantity;
        private string _adjustmentReason = "Corrected";
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private bool _isStatusSuccess;
        private AlertBatchItem? _selectedAlertItem;

        public InventoryAdjustmentViewModel(MedicineRepository medicineRepo, BatchRepository batchRepo, AuditRepository auditRepo, AuthService authService, AuthorizationService authorizationService)
        {
            _medicineRepo = medicineRepo;
            _batchRepo = batchRepo;
            _auditRepo = auditRepo;
            _authService = authService;
            _authorizationService = authorizationService;

            MedicineResults = new ObservableCollection<Medicine>();
            Batches = new ObservableCollection<InventoryBatch>();
            LowStockItems = new ObservableCollection<AlertBatchItem>();
            NearExpiryItems = new ObservableCollection<AlertBatchItem>();
            Reasons = new List<string> { "Damaged", "Theft", "Expired", "Corrected", "Return to Supplier" };

            SearchCommand = new AsyncRelayCommand(SearchMedicinesAsync);
            AdjustStockCommand = new AsyncRelayCommand(ExecuteAdjustmentAsync, _ => CanAdjust());

            _ = LoadAlertsAsync();
        }

        public ObservableCollection<Medicine> MedicineResults { get; }
        public ObservableCollection<InventoryBatch> Batches { get; }
        public ObservableCollection<AlertBatchItem> LowStockItems { get; }
        public ObservableCollection<AlertBatchItem> NearExpiryItems { get; }
        public List<string> Reasons { get; }

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = SearchMedicinesAsync(); }
        }

        public Medicine? SelectedMedicine
        {
            get => _selectedMedicine;
            set 
            { 
                if (SetProperty(ref _selectedMedicine, value)) 
                {
                    _ = LoadBatchesAsync();
                }
            }
        }

        public InventoryBatch? SelectedBatch
        {
            get => _selectedBatch;
            set 
            { 
                if (SetProperty(ref _selectedBatch, value)) 
                {
                    NewQuantity = value?.RemainingUnits ?? 0;
                    ((AsyncRelayCommand)AdjustStockCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public int NewQuantity
        {
            get => _newQuantity;
            set { if (SetProperty(ref _newQuantity, value)) ((AsyncRelayCommand)AdjustStockCommand).RaiseCanExecuteChanged(); }
        }

        public string AdjustmentReason
        {
            get => _adjustmentReason;
            set { SetProperty(ref _adjustmentReason, value); }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsStatusSuccess { get => _isStatusSuccess; set => SetProperty(ref _isStatusSuccess, value); }

        public AlertBatchItem? SelectedAlertItem
        {
            get => _selectedAlertItem;
            set
            {
                if (SetProperty(ref _selectedAlertItem, value) && value != null)
                {
                    _ = LoadFromAlertAsync(value);
                }
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand AdjustStockCommand { get; }

        private async Task LoadAlertsAsync()
        {
            try
            {
                var lowStock = await _batchRepo.GetLowStockAsync();
                LowStockItems.Clear();
                foreach (var i in lowStock) LowStockItems.Add(i);

                var nearExpiry = await _batchRepo.GetNearExpiryAsync(30);
                NearExpiryItems.Clear();
                foreach (var i in nearExpiry) NearExpiryItems.Add(i);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("InventoryAdjustmentViewModel.LoadAlertsAsync failed", ex);
            }
        }

        private System.Threading.CancellationTokenSource? _searchCts;

        private async Task SearchMedicinesAsync()
        {
            _searchCts?.Cancel();
            _searchCts = new System.Threading.CancellationTokenSource();
            var token = _searchCts.Token;

            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    MedicineResults.Clear();
                    return;
                }

                // Debounce
                await Task.Delay(300, token);
                
                var results = await _medicineRepo.SearchAsync(SearchText);
                
                if (token.IsCancellationRequested) return;

                MedicineResults.Clear();
                foreach (var r in results) MedicineResults.Add(r);
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                AppLogger.LogError("InventoryAdjustmentViewModel.SearchMedicinesAsync failed", ex);
            }
        }

        private async Task LoadBatchesAsync()
        {
            Batches.Clear();
            SelectedBatch = null;
            if (SelectedMedicine == null) return;

            var batches = await _batchRepo.GetByMedicineIdAsync(SelectedMedicine.Id);
            foreach (var b in batches) Batches.Add(b);
        }

        private async Task LoadFromAlertAsync(AlertBatchItem item)
        {
            SearchText = item.MedicineName;
            // First select the medicine
            var results = await _medicineRepo.SearchAsync(item.MedicineName);
            var med = results.FirstOrDefault(m => m.Id == item.MedicineId);
            if (med != null)
            {
                SelectedMedicine = med;
                await LoadBatchesAsync();
                
                // If it's a specific batch item (near expiry), select that batch
                if (item.BatchId.HasValue)
                {
                    var batch = Batches.FirstOrDefault(b => b.Id == item.BatchId.Value);
                    if (batch != null) SelectedBatch = batch;
                }
            }
        }

        private bool CanAdjust() => SelectedBatch != null && !_isBusy;

        private async Task ExecuteAdjustmentAsync()
        {
            if (SelectedBatch == null || _authService.CurrentUser == null) return;

            IsBusy = true;
            StatusMessage = "Adjusting stock...";
            try
            {
                await _batchRepo.UpdateStockManualAsync(SelectedBatch.Id, NewQuantity, AdjustmentReason, _authService.CurrentUser.Id, _auditRepo);
                
                IsStatusSuccess = true;
                StatusMessage = "✅ Stock adjustment successful.";
                _ = LoadBatchesAsync(); // Refresh
            }
            catch (Exception ex)
            {
                IsStatusSuccess = false;
                StatusMessage = $"⚠ Error: {ex.Message}";
            }
            finally { IsBusy = false; }
        }
    }
}
