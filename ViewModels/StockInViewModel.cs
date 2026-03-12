using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;
using Microsoft.UI.Dispatching;

namespace DChemist.ViewModels
{
    public class StockInViewModel : ViewModelBase
    {
        private readonly MedicineRepository  _medicineRepo;
        private readonly BatchRepository     _batchRepo;
        private readonly SupplierRepository  _supplierRepo;
        private readonly InventoryEventBus   _eventBus;
        private readonly IDialogService      _dialogService;
        private readonly DispatcherQueue     _dispatcher;

        public StockInViewModel(
            MedicineRepository medicineRepo,
            BatchRepository batchRepo,
            SupplierRepository supplierRepo,
            InventoryEventBus eventBus,
            IDialogService dialogService)
        {
            _medicineRepo  = medicineRepo;
            _batchRepo     = batchRepo;
            _supplierRepo  = supplierRepo;
            _eventBus      = eventBus;
            _dialogService = dialogService;
            _dispatcher    = DispatcherQueue.GetForCurrentThread();

            ReceivingItems = new ObservableCollection<ReceivingItem>();
            Suppliers      = new ObservableCollection<Supplier>();

            LookupBarcodeCommand = new AsyncRelayCommand(async _ => await ExecuteLookupBarcodeAsync());
            AddToListCommand     = new AsyncRelayCommand(async _ => await ExecuteAddToListAsync(),
                                                        _         => FoundMedicine != null);
            RemoveItemCommand    = new RelayCommand(item => ReceivingItems.Remove(item as ReceivingItem));
            ClearEntryCommand    = new RelayCommand(_ => ClearEntry());
            SaveAllCommand       = new AsyncRelayCommand(async _ => await ExecuteSaveAllAsync(),
                                                        _         => ReceivingItems.Count > 0);

            PurchaseDate = DateTimeOffset.Now;
            _ = LoadSuppliersAsync();
        }

        // ── Header fields ─────────────────────────────────────────────────
        private string _invoiceNumber = string.Empty;
        public string InvoiceNumber
        {
            get => _invoiceNumber;
            set => SetProperty(ref _invoiceNumber, value);
        }

        private DateTimeOffset? _purchaseDate;
        public DateTimeOffset? PurchaseDate
        {
            get => _purchaseDate;
            set => SetProperty(ref _purchaseDate, value);
        }

        public ObservableCollection<Supplier> Suppliers { get; }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        // ── Barcode & preview ─────────────────────────────────────────────
        private string _barcodeText = string.Empty;
        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }

        private Medicine? _foundMedicine;
        public Medicine? FoundMedicine
        {
            get => _foundMedicine;
            set
            {
                if (SetProperty(ref _foundMedicine, value))
                {
                    OnPropertyChanged(nameof(HasFoundMedicine));
                    OnPropertyChanged(nameof(FoundName));
                    OnPropertyChanged(nameof(FoundManufacturer));
                    OnPropertyChanged(nameof(FoundCategory));
                    OnPropertyChanged(nameof(FoundBaseUnit));
                    OnPropertyChanged(nameof(AvailableUnits));
                    ((AsyncRelayCommand)AddToListCommand).RaiseCanExecuteChanged();
                    // Pre-populate the unit selector
                    if (value != null)
                        SelectedUnit = Capitalize(value.BaseUnit);
                }
            }
        }
        public bool HasFoundMedicine => FoundMedicine != null;

        // ── Safe proxy props — used by XAML to avoid null-chain bindings ──
        public string FoundName         => FoundMedicine?.Name            ?? string.Empty;
        public string FoundManufacturer => FoundMedicine?.ManufacturerName ?? string.Empty;
        public string FoundCategory     => FoundMedicine?.CategoryName     ?? string.Empty;
        public string FoundBaseUnit     => FoundMedicine?.BaseUnit         ?? string.Empty;

        public System.Collections.Generic.List<string> AvailableUnits
        {
            get
            {
                if (FoundMedicine?.AvailableUnits is { } units)
                    return units.Select(u => u.Label).ToList();
                return new System.Collections.Generic.List<string> { "Unit" };
            }
        }

        // ── Entry form ────────────────────────────────────────────────────
        private string _batchNumber = string.Empty;
        public string BatchNumber
        {
            get => _batchNumber;
            set => SetProperty(ref _batchNumber, value);
        }

        private DateTimeOffset? _expiryDate;
        public DateTimeOffset? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        private string _selectedUnit = string.Empty;
        public string SelectedUnit
        {
            get => _selectedUnit;
            set => SetProperty(ref _selectedUnit, value);
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set => SetProperty(ref _quantity, value);
        }

        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set => SetProperty(ref _purchasePrice, value);
        }

        private decimal _sellingPrice;
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set => SetProperty(ref _sellingPrice, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        // ── Receiving List ────────────────────────────────────────────────
        public ObservableCollection<ReceivingItem> ReceivingItems { get; }

        // ── Commands ──────────────────────────────────────────────────────
        public ICommand LookupBarcodeCommand { get; }
        public ICommand AddToListCommand     { get; }
        public ICommand RemoveItemCommand    { get; }
        public ICommand ClearEntryCommand    { get; }
        public ICommand SaveAllCommand       { get; }

        // ── Implementation ────────────────────────────────────────────────

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var list = await _supplierRepo.GetAllAsync();
                _dispatcher.TryEnqueue(() =>
                {
                    Suppliers.Clear();
                    foreach (var s in list) Suppliers.Add(s);
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("StockInViewModel.LoadSuppliersAsync", ex);
            }
        }

        private async Task ExecuteLookupBarcodeAsync()
        {
            var barcode = BarcodeText.Trim();
            if (string.IsNullOrEmpty(barcode)) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            FoundMedicine = null;
            try
            {
                var medicine = await _medicineRepo.GetByBarcodeAsync(barcode);
                if (medicine != null)
                {
                    FoundMedicine = medicine;
                    StatusMessage = $"✔ Found: {medicine.Name}";
                }
                else
                {
                    StatusMessage = $"⚠ Barcode '{barcode}' not found.";
                    // Ask user if they want to add a new medicine
                    bool add = await _dialogService.ShowConfirmationAsync(
                        "Medicine Not Found",
                        $"Barcode '{barcode}' was not found.\nWould you like to add a new medicine?",
                        "Add Medicine", "Cancel");

                    if (add)
                        await _dialogService.ShowInventoryDialogAsync();
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Lookup failed — check database connection.";
                AppLogger.LogError("StockInViewModel.ExecuteLookupBarcodeAsync", ex);
            }
            finally
            {
                IsBusy = false;
                BarcodeText = string.Empty; // auto-clear for next scan
            }
        }

        private async Task ExecuteAddToListAsync()
        {
            if (FoundMedicine == null) return;

            // ── Validation ────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(BatchNumber))
            {
                StatusMessage = "⚠ Batch number is required.";
                return;
            }
            if (Quantity <= 0)
            {
                StatusMessage = "⚠ Quantity must be greater than 0.";
                return;
            }
            if (!ExpiryDate.HasValue)
            {
                StatusMessage = "⚠ Expiry date is required.";
                return;
            }
            if (PurchasePrice <= 0)
            {
                StatusMessage = "⚠ Purchase price must be greater than 0.";
                return;
            }

            // ── Duplicate check ────────────────────────────────────────────
            var existing = ReceivingItems.FirstOrDefault(r =>
                r.MedicineId == FoundMedicine.Id &&
                string.Equals(r.BatchNumber, BatchNumber, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                bool merge = await _dialogService.ShowConfirmationAsync(
                    "Duplicate Batch",
                    $"'{FoundMedicine.Name}' with batch '{BatchNumber}' is already in the list.\nMerge quantities?",
                    "Merge", "Add Separate Row");

                if (merge)
                {
                    existing.Quantity += Quantity;
                    ClearEntry();
                    StatusMessage = "✔ Quantities merged.";
                    return;
                }
            }

            // ── Build unit conversion factor ───────────────────────────────
            int conversionFactor = 1;
            if (string.Equals(SelectedUnit, "Strip", StringComparison.OrdinalIgnoreCase))
                conversionFactor = FoundMedicine.StripSize ?? 1;
            else if (string.Equals(SelectedUnit, "Box", StringComparison.OrdinalIgnoreCase))
                conversionFactor = FoundMedicine.BoxSize ?? 1;

            var item = new ReceivingItem
            {
                MedicineId       = FoundMedicine.Id,
                MedicineName     = FoundMedicine.Name,
                ManufacturerName = FoundMedicine.ManufacturerName ?? string.Empty,
                BaseUnit         = FoundMedicine.BaseUnit,
                StripSize        = FoundMedicine.StripSize,
                BoxSize          = FoundMedicine.BoxSize,
                BatchNumber      = BatchNumber,
                SupplierId       = SelectedSupplier?.Id ?? 0,
                SupplierName     = SelectedSupplier?.Name ?? string.Empty,
                SelectedUnit     = SelectedUnit,
                Quantity         = Quantity,
                PurchasePrice    = PurchasePrice,
                SellingPrice     = SellingPrice > 0 ? SellingPrice : PurchasePrice * 1.2m
            };
            // ExpiryDate: convert DateTimeOffset? → DateTime? for the model
            item.ExpiryDate = ExpiryDate.HasValue ? ExpiryDate.Value.DateTime : (DateTime?)null;

            ReceivingItems.Add(item);
            ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
            StatusMessage = $"✔ Added: {item.MedicineName} × {item.Quantity} {item.SelectedUnit}(s) = {item.BaseUnitsToStore} {item.BaseUnit}(s)";
            ClearEntry();
        }

        private async Task ExecuteSaveAllAsync()
        {
            if (ReceivingItems.Count == 0)
            {
                StatusMessage = "⚠ Nothing to save — add at least one item.";
                return;
            }

            bool confirmed = await _dialogService.ShowConfirmationAsync(
                "Save Stock",
                $"Save {ReceivingItems.Count} item(s) to inventory?",
                "Save All", "Cancel");
            if (!confirmed) return;

            IsBusy = true;
            StatusMessage = "Saving…";
            try
            {
                var batches = ReceivingItems.Select(r => new InventoryBatch
                {
                    MedicineId    = r.MedicineId,
                    SupplierId    = r.SupplierId,
                    BatchNumber   = r.BatchNumber,
                    PurchasePrice = r.PurchasePrice,
                    SellingPrice  = r.SellingPrice,
                    StockQty      = r.BaseUnitsToStore,     // always base units
                    ExpiryDate    = r.ExpiryDate!.Value
                }).ToList();

                await _batchRepo.AddBulkAsync(batches);

                // Notify inventory page to refresh
                _eventBus.Publish(InventoryChangeType.MedicineAdded);

                int total = ReceivingItems.Sum(r => r.BaseUnitsToStore);
                StatusMessage = $"✔ Saved {batches.Count} batch(es) — {total} base units added to inventory.";
                ReceivingItems.Clear();
                ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
                ClearEntry();
            }
            catch (DataAccessException ex)
            {
                StatusMessage = $"✘ {ex.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Unexpected error during save.";
                AppLogger.LogError("StockInViewModel.ExecuteSaveAllAsync", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearEntry()
        {
            FoundMedicine = null;
            BatchNumber   = string.Empty;
            ExpiryDate    = null;
            SelectedUnit  = string.Empty;
            Quantity      = 1;
            PurchasePrice = 0;
            SellingPrice  = 0;
            BarcodeText   = string.Empty;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
