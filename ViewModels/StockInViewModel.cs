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
            ReceivingItems.CollectionChanged += (s, e) => OnPropertyChanged(nameof(GrandTotal));
            Suppliers      = new ObservableCollection<Supplier>();

            LookupBarcodeCommand = new AsyncRelayCommand(async _ => await ExecuteLookupBarcodeAsync());
            AddToListCommand     = new AsyncRelayCommand(async _ => await ExecuteAddToListAsync());
            RemoveItemCommand    = new RelayCommand(item => 
            {
                if (item is ReceivingItem ri) ReceivingItems.Remove(ri);
            });
            ClearEntryCommand    = new RelayCommand(_ => ClearEntry());
            SaveAllCommand       = new AsyncRelayCommand(async _ => await ExecuteSaveAllAsync(),
                                                        _         => ReceivingItems.Count > 0);
            SaveNewMedicineCommand = new AsyncRelayCommand(async _ => await ExecuteSaveNewMedicineAsync());
            RegisterNewMedCommand = new RelayCommand(_ => {
                IsNewMedFormVisible = true;
                FoundMedicine = null;
                NewMedBarcode = string.Empty;
                NewMedName = string.Empty;
                NewMedGeneric = string.Empty;
            });

            SessionInvoiceDate = DateTimeOffset.Now;
            _ = LoadSuppliersAsync();
            _ = LoadMetaDataAsync();
        }

        private async Task LoadMetaDataAsync()
        {
            try {
                _dispatcher.TryEnqueue(() => {
                    Categories.Clear();
                    Categories.Add(new Category { Name = "Tablets" });
                    Categories.Add(new Category { Name = "Syrup" });
                    Categories.Add(new Category { Name = "Injection" });
                    Categories.Add(new Category { Name = "Drops" });
                    Categories.Add(new Category { Name = "Capsules" });
                    SelectedCategory = Categories.FirstOrDefault();

                    Manufacturers.Clear();
                    Manufacturers.Add(new Manufacturer { Name = "GSK" });
                    Manufacturers.Add(new Manufacturer { Name = "Abbott" });
                    Manufacturers.Add(new Manufacturer { Name = "Pfizer" });
                    Manufacturers.Add(new Manufacturer { Name = "Getz" });
                    Manufacturers.Add(new Manufacturer { Name = "Sami" });
                    SelectedManufacturer = Manufacturers.FirstOrDefault();
                });
            } catch { }
            await Task.CompletedTask;
        }

        private async Task ExecuteSaveNewMedicineAsync()
        {
            if (string.IsNullOrWhiteSpace(NewMedName)) {
                StatusMessage = "⚠ Medicine name is required.";
                return;
            }

            IsBusy = true;
            try {
                var med = new Medicine {
                    Name = NewMedName,
                    GenericName = NewMedGeneric,
                    Barcode = NewMedBarcode,
                    CategoryName = SelectedCategory?.Name ?? "General",
                    ManufacturerName = SelectedManufacturer?.Name ?? "Unknown",
                    SupplierName = SessionSupplierName, // Pass current session supplier if any
                    StockQty = 0, // Just creating the record
                    PurchasePrice = 0,
                    SellingPrice = 0
                };
                await _medicineRepo.AddAsync(med);
                
                // Now lookup it back to get the ID and full object
                var saved = await _medicineRepo.GetByBarcodeAsync(NewMedBarcode);
                if (saved != null) {
                    FoundMedicine = saved;
                    IsNewMedFormVisible = false;
                    StatusMessage = $"✔ Registered: {saved.Name}";
                    if (IsAutoAddEnabled) await ExecuteAddToListAsync();
                }
            } catch (Exception ex) {
                StatusMessage = "✘ Failed to register medicine.";
                AppLogger.LogError("StockIn.SaveNew", ex);
            } finally {
                IsBusy = false;
            }
        }

        // ── Header fields ─────────────────────────────────────────────────
        private string _sessionInvoiceNo = string.Empty;
        public string SessionInvoiceNo
        {
            get => _sessionInvoiceNo;
            set => SetProperty(ref _sessionInvoiceNo, value);
        }

        private DateTimeOffset? _sessionInvoiceDate = DateTimeOffset.Now;
        public DateTimeOffset? SessionInvoiceDate
        {
            get => _sessionInvoiceDate;
            set => SetProperty(ref _sessionInvoiceDate, value);
        }

        private string _sessionSupplierName = string.Empty;
        public string SessionSupplierName
        {
            get => _sessionSupplierName;
            set => SetProperty(ref _sessionSupplierName, value);
        }

        public ObservableCollection<Supplier> Suppliers { get; }

        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier
        {
            get => _selectedSupplier;
            set
            {
                if (SetProperty(ref _selectedSupplier, value))
                {
                    if (value != null) SessionSupplierName = value.Name;
                }
            }
        }

        // ── Barcode & preview ─────────────────────────────────────────────
        private string _barcodeText = string.Empty;
        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }

        private bool _isContinuousScanMode;
        public bool IsContinuousScanMode
        {
            get => _isContinuousScanMode;
            set => SetProperty(ref _isContinuousScanMode, value);
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
                    ((AsyncRelayCommand)AddToListCommand).RaiseCanExecuteChanged();
                }
            }
        }
        public bool HasFoundMedicine => FoundMedicine != null;

        // ── Safe proxy props — used by XAML to avoid null-chain bindings ──
        public string FoundName         => FoundMedicine?.Name            ?? string.Empty;
        public string FoundManufacturer => FoundMedicine?.ManufacturerName ?? string.Empty;
        public string FoundCategory     => FoundMedicine?.CategoryName     ?? string.Empty;

        // ── Manual Search ─────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    if (value.Length >= 2)
                        _ = ExecuteSearchAsync(value);
                }
            }
        }

        public ObservableCollection<Medicine> SearchSuggestions { get; } = new();

        private async Task ExecuteSearchAsync(string query)
        {
            try
            {
                var list = await _medicineRepo.SearchAsync(query);
                _dispatcher.TryEnqueue(() =>
                {
                    SearchSuggestions.Clear();
                    foreach (var m in list) SearchSuggestions.Add(m);
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("StockInViewModel.ExecuteSearchAsync", ex);
            }
        }

        public void SelectMedicine(Medicine? medicine)
        {
            if (medicine != null)
            {
                FoundMedicine = medicine;
                StatusMessage = $"✔ Selected: {medicine.Name}";
                SearchText = string.Empty; // clear search
                SearchSuggestions.Clear();
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

        // ── Automation & Visibility ──────────────────────────────────────
        private bool _isAutoAddEnabled = true;
        public bool IsAutoAddEnabled
        {
            get => _isAutoAddEnabled;
            set => SetProperty(ref _isAutoAddEnabled, value);
        }

        private bool _isNewMedFormVisible;
        public bool IsNewMedFormVisible
        {
            get => _isNewMedFormVisible;
            set => SetProperty(ref _isNewMedFormVisible, value);
        }

        // ── Form Entry Proxies (String Bridge) ────────────────────────────
        private int _quantityUnits = 1;
        public int QuantityUnits
        {
            get => _quantityUnits;
            set
            {
                if (SetProperty(ref _quantityUnits, value))
                {
                    OnPropertyChanged(nameof(PurchasePricePerUnit));
                    OnPropertyChanged(nameof(SellingPricePerUnit));
                    OnPropertyChanged(nameof(UnitProfitText));
                    OnPropertyChanged(nameof(ProfitMarginText));
                }
            }
        }

        private decimal _purchaseTotalPrice;
        public decimal PurchaseTotalPrice
        {
            get => _purchaseTotalPrice;
            set
            {
                if (SetProperty(ref _purchaseTotalPrice, value))
                {
                    OnPropertyChanged(nameof(PurchasePricePerUnit));
                    OnPropertyChanged(nameof(UnitProfitText));
                    OnPropertyChanged(nameof(ProfitMarginText));
                }
            }
        }

        private decimal _totalSellingPrice;
        public decimal TotalSellingPrice
        {
            get => _totalSellingPrice;
            set
            {
                if (SetProperty(ref _totalSellingPrice, value))
                {
                    OnPropertyChanged(nameof(SellingPricePerUnit));
                    OnPropertyChanged(nameof(UnitProfitText));
                    OnPropertyChanged(nameof(ProfitMarginText));
                }
            }
        }

        public decimal PurchasePricePerUnit => QuantityUnits > 0 ? PurchaseTotalPrice / QuantityUnits : 0;
        public decimal SellingPricePerUnit  => QuantityUnits > 0 ? TotalSellingPrice / QuantityUnits : 0;

        // Bridge properties for UI (String based inputs)
        public string QuantityUnitsText
        {
            get => _quantityUnits.ToString();
            set
            {
                if (int.TryParse(value, out int res)) QuantityUnits = res;
                OnPropertyChanged(nameof(QuantityUnitsText));
            }
        }

        public string PurchaseTotalPriceText
        {
            get => _purchaseTotalPrice.ToString("G29");
            set
            {
                if (decimal.TryParse(value, out decimal res)) PurchaseTotalPrice = res;
                OnPropertyChanged(nameof(PurchaseTotalPriceText));
            }
        }

        public string TotalSellingPriceText
        {
            get => _totalSellingPrice.ToString("G29");
            set
            {
                if (decimal.TryParse(value, out decimal res)) TotalSellingPrice = res;
                OnPropertyChanged(nameof(TotalSellingPriceText));
            }
        }

        public string PurchasePricePerUnitText => PurchasePricePerUnit.ToString("N2");
        public string SellingPricePerUnitText  => SellingPricePerUnit.ToString("N2");

        public string UnitProfitText => (SellingPricePerUnit - PurchasePricePerUnit).ToString("N2");
        public string ProfitMarginText => PurchasePricePerUnit > 0 
            ? (((SellingPricePerUnit - PurchasePricePerUnit) / PurchasePricePerUnit) * 100).ToString("F0") + "%" 
            : "0%";

        // ── Embedded New Medicine Form ────────────────────────────────
        private string _newMedName = string.Empty;
        public string NewMedName { get => _newMedName; set => SetProperty(ref _newMedName, value); }

        private string _newMedGeneric = string.Empty;
        public string NewMedGeneric { get => _newMedGeneric; set => SetProperty(ref _newMedGeneric, value); }

        private string _newMedBarcode = string.Empty;
        public string NewMedBarcode { get => _newMedBarcode; set => SetProperty(ref _newMedBarcode, value); }

        public ObservableCollection<Category> Categories { get; } = new();
        private Category? _selectedCategory;
        public Category? SelectedCategory { get => _selectedCategory; set => SetProperty(ref _selectedCategory, value); }

        public ObservableCollection<Manufacturer> Manufacturers { get; } = new();
        private Manufacturer? _selectedManufacturer;
        public Manufacturer? SelectedManufacturer { get => _selectedManufacturer; set => SetProperty(ref _selectedManufacturer, value); }

        public ICommand SaveNewMedicineCommand { get; }
        public ICommand RegisterNewMedCommand { get; }

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
        
        public decimal GrandTotal => ReceivingItems.Sum(x => x.PurchaseTotalPrice);

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

            // Immediate reset for next scan speed
            BarcodeText = string.Empty; 

            try
            {
                var medicine = await _medicineRepo.GetByBarcodeAsync(barcode);
                if (medicine != null)
                {
                    FoundMedicine = medicine;
                    StatusMessage = $"✔ Found: {medicine.Name}";
                    
                    if (IsAutoAddEnabled)
                    {
                        // Auto-add uses default values for session speed
                        await ExecuteAddToListAsync();
                    }
                }
                else
                {
                    StatusMessage = $"⚠ Medicine not found: {barcode}. Add manually.";
                    NewMedBarcode = barcode;
                    IsNewMedFormVisible = true;
                    FoundMedicine = null;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Lookup failed.";
                AppLogger.LogError("StockInViewModel.ExecuteLookupBarcodeAsync", ex);
            }
        }

        private async Task ExecuteAddToListAsync()
        {
            if (FoundMedicine == null) return;

            // ── Validation ────────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(BatchNumber))
            {
                StatusMessage = "⚠ Batch number required.";
                return;
            }
            if (!ExpiryDate.HasValue)
            {
                StatusMessage = "⚠ Expiry date required.";
                return;
            }
            if (QuantityUnits <= 0)
            {
                StatusMessage = "⚠ Quantity must be > 0.";
                return;
            }

            var item = new ReceivingItem
            {
                MedicineId         = FoundMedicine.Id,
                MedicineName       = FoundMedicine.Name,
                ManufacturerName   = FoundMedicine.ManufacturerName ?? string.Empty,
                BatchNo            = BatchNumber,
                SupplierName       = SessionSupplierName,
                InvoiceNo          = SessionInvoiceNo,
                InvoiceDate        = SessionInvoiceDate?.DateTime,
                QuantityUnits      = QuantityUnits,
                PurchaseTotalPrice = PurchaseTotalPrice,
                TotalSellingPrice  = TotalSellingPrice,
                ExpiryDate         = ExpiryDate.Value.DateTime
            };

            ReceivingItems.Add(item);
            StatusMessage = $"✔ Added: {item.MedicineName}";
            
            // Auto-notification for 'Save All' button state
            ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
            
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
                // Resolve Supplier once for the whole session
                int finalSupplierId = SelectedSupplier?.Id ?? 0;
                if (finalSupplierId == 0 && !string.IsNullOrWhiteSpace(SessionSupplierName))
                {
                    var resolvedSupplier = await _supplierRepo.GetOrCreateByNameAsync(SessionSupplierName);
                    finalSupplierId = resolvedSupplier.Id;
                }

                var batches = ReceivingItems.Select(r => new InventoryBatch
                {
                    MedicineId         = r.MedicineId,
                    SupplierId         = finalSupplierId,
                    BatchNo            = r.BatchNo,
                    InvoiceNo          = r.InvoiceNo,
                    InvoiceDate        = r.InvoiceDate,
                    QuantityUnits      = r.QuantityUnits,
                    PurchaseTotalPrice = r.PurchaseTotalPrice,
                    UnitCost           = r.UnitCost,
                    SellingPrice       = r.SellingPrice,
                    RemainingUnits     = r.QuantityUnits,
                    ExpiryDate         = r.ExpiryDate!.Value
                }).ToList();

                await _batchRepo.AddBulkAsync(batches);

                // Notify inventory page to refresh
                _eventBus.Publish(InventoryChangeType.MedicineAdded);

                int total = ReceivingItems.Sum(r => r.QuantityUnits);
                StatusMessage = $"✔ Saved {batches.Count} batch(es) — {total} units added to inventory.";
                ReceivingItems.Clear();
                ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
                ClearEntry();

                // Clear session fields after successful mass save
                SessionInvoiceNo = string.Empty;
                SessionSupplierName = string.Empty;
                SelectedSupplier = null;
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
            FoundMedicine          = null;
            BatchNumber            = string.Empty;
            ExpiryDate             = null;
            QuantityUnitsText      = "1";
            PurchaseTotalPriceText = "0";
            TotalSellingPriceText  = "0";
            BarcodeText            = string.Empty;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
