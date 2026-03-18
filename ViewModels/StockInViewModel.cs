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
        public event EventHandler<string>? RequestFocus;
        private readonly MedicineRepository     _medicineRepo;
        private readonly BatchRepository        _batchRepo;
        private readonly SupplierRepository     _supplierRepo;
        private readonly InventoryEventBus      _eventBus;
        private readonly IDialogService          _dialogService;
        private readonly BarcodeLookupService    _barcodeLookupService;
        private readonly DispatcherQueue         _dispatcher;

        public StockInViewModel(
            MedicineRepository medicineRepo,
            BatchRepository batchRepo,
            SupplierRepository supplierRepo,
            InventoryEventBus eventBus,
            IDialogService dialogService,
            BarcodeLookupService barcodeLookupService)
        {
            _medicineRepo     = medicineRepo;
            _batchRepo        = batchRepo;
            _supplierRepo     = supplierRepo;
            _eventBus         = eventBus;
            _dialogService    = dialogService;
            _barcodeLookupService = barcodeLookupService;
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

            SessionInvoiceDate = DateTimeOffset.Now;
            _ = LoadSuppliersAsync();
            _ = LoadMetaDataAsync();
        }

        private async Task LoadMetaDataAsync()
        {
            await Task.CompletedTask;
        }
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

        private string _entryName = string.Empty;
        public string EntryName { get => _entryName; set => SetProperty(ref _entryName, value); }

        private string _entryDosage = string.Empty; 
        public string EntryDosage { get => _entryDosage; set => SetProperty(ref _entryDosage, value); }
        
        private decimal _entryGst = 0;
        public decimal EntryGst { get => _entryGst; set => SetProperty(ref _entryGst, value); }

        public string EntryGstText
        {
            get => _entryGst.ToString("G29");
            set { if (decimal.TryParse(value, out decimal res)) EntryGst = res; OnPropertyChanged(nameof(EntryGstText)); }
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
                    if (value != null)
                    {
                        EntryName = value.Name;
                        EntryDosage = value.Strength ?? string.Empty;
                        EntryGst = value.GstPercent;
                        OnPropertyChanged(nameof(EntryGstText));
                    }
                }
            }
        }
        public bool HasFoundMedicine => FoundMedicine != null;

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    if (value.Length >= 2) _ = ExecuteSearchAsync(value);
                }
            }
        }

        public ObservableCollection<Medicine> SearchSuggestions { get; } = new();

        private async Task ExecuteSearchAsync(string query)
        {
            try {
                var list = await _medicineRepo.SearchAsync(query);
                _dispatcher.TryEnqueue(() => {
                    SearchSuggestions.Clear();
                    foreach (var m in list) SearchSuggestions.Add(m);
                });
            } catch (Exception ex) { AppLogger.LogError("StockIn.Search", ex); }
        }

        public void SelectMedicine(Medicine? medicine)
        {
            if (medicine != null)
            {
                FoundMedicine = medicine;
                StatusMessage = $"✔ Loaded: {medicine.Name}";
                SearchText = string.Empty;
                SearchSuggestions.Clear();
            }
        }

        private string _batchNumber = string.Empty;
        public string BatchNumber { get => _batchNumber; set => SetProperty(ref _batchNumber, value); }

        private DateTimeOffset? _expiryDate;
        public DateTimeOffset? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }

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

        public string QuantityUnitsText
        {
            get => _quantityUnits.ToString();
            set { if (int.TryParse(value, out int res)) QuantityUnits = res; OnPropertyChanged(nameof(QuantityUnitsText)); }
        }

        public string PurchaseTotalPriceText
        {
            get => _purchaseTotalPrice.ToString("G29");
            set { if (decimal.TryParse(value, out decimal res)) PurchaseTotalPrice = res; OnPropertyChanged(nameof(PurchaseTotalPriceText)); }
        }

        public string TotalSellingPriceText
        {
            get => _totalSellingPrice.ToString("G29");
            set { if (decimal.TryParse(value, out decimal res)) TotalSellingPrice = res; OnPropertyChanged(nameof(TotalSellingPriceText)); }
        }

        public string PurchasePricePerUnitText => PurchasePricePerUnit.ToString("N2");
        public string SellingPricePerUnitText  => SellingPricePerUnit.ToString("N2");
        public string UnitProfitText => (SellingPricePerUnit - PurchasePricePerUnit).ToString("N2");
        public string ProfitMarginText => PurchasePricePerUnit > 0 
            ? (((SellingPricePerUnit - PurchasePricePerUnit) / PurchasePricePerUnit) * 100).ToString("F0") + "%" 
            : "0%";

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _isAutoAddEnabled = false; 
        public bool IsAutoAddEnabled { get => _isAutoAddEnabled; set => SetProperty(ref _isAutoAddEnabled, value); }

        public ObservableCollection<ReceivingItem> ReceivingItems { get; }
        
        public decimal GrandTotal => ReceivingItems.Sum(x => x.PurchaseTotalPrice);
        public ICommand LookupBarcodeCommand { get; }
        public ICommand AddToListCommand     { get; }
        public ICommand RemoveItemCommand    { get; }
        public ICommand ClearEntryCommand    { get; }
        public ICommand SaveAllCommand       { get; }

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
            if (string.IsNullOrEmpty(barcode)) 
            {
                AppLogger.LogInfo("StockIn.Lookup: Empty barcode.");
                return;
            }

            AppLogger.LogInfo($"StockIn.Lookup: Scanning '{barcode}'...");
            
            Medicine? medicine = null;
            try
            {
                medicine = await _medicineRepo.GetByBarcodeAsync(barcode);
                if (medicine != null)
                {
                    AppLogger.LogInfo($"StockIn.Lookup: Found '{medicine.Name}' in local DB.");
                    FoundMedicine = medicine;
                    StatusMessage = $"✔ Found: {medicine.Name}";
                    
                    if (IsAutoAddEnabled) 
                    {
                        AppLogger.LogInfo("StockIn.Lookup: Auto-Add enabled. Adding to list...");
                        await ExecuteAddToListAsync();
                    }
                }
                else
                {
                    AppLogger.LogInfo($"StockIn.Lookup: Barcode '{barcode}' not found locally. Trying external API...");
                    StatusMessage = $"Fetching details...";
                    
                    var externalMed = await _barcodeLookupService.FetchMedicineFromExternalApiAsync(barcode);
                    if (externalMed != null)
                    {
                        AppLogger.LogInfo($"StockIn.Lookup: External API found '{externalMed.Name}'. Saving to local DB...");
                        
                        var existing = await _medicineRepo.SearchAsync(externalMed.Name);
                        var match = existing.FirstOrDefault(m => m.Name.Equals(externalMed.Name, StringComparison.OrdinalIgnoreCase));
                        
                        if (match == null)
                        {
                            externalMed = await _medicineRepo.AddAsync(externalMed);
                        }
                        else
                        {
                            match.Barcode = barcode;
                            await _medicineRepo.UpdateAsync(match);
                            externalMed = match;
                        }
                        
                        FoundMedicine = externalMed;
                        StatusMessage = $"✔ Fetched: {externalMed.Name}";
                        
                        if (IsAutoAddEnabled) 
                        {
                            AppLogger.LogInfo("StockIn.Lookup: External API auto-add triggered.");
                            await ExecuteAddToListAsync();
                        }
                    }
                    else
                    {
                        AppLogger.LogInfo($"StockIn.Lookup: External API failed for '{barcode}'.");
                        StatusMessage = $"ℹ New Barcode: {barcode}";
                        ClearEntryInternal(false); 
                        RequestFocus?.Invoke(this, "MedicineName");
                    }
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Lookup failed.";
                AppLogger.LogError("StockIn.Lookup", ex);
            }
        }

        private async Task ExecuteAddToListAsync()
        {
            AppLogger.LogInfo($"StockIn.AddToList: Starting for '{EntryName}'...");
            
            if (string.IsNullOrWhiteSpace(EntryName)) 
            { 
                StatusMessage = "⚠ Medicine name is required."; 
                AppLogger.LogWarning("StockIn.AddToList: Validation failed - Name is empty.");
                return; 
            }
            if (string.IsNullOrWhiteSpace(BatchNumber)) 
            { 
                if (IsAutoAddEnabled)
                {
                    BatchNumber = "B-" + DateTime.Now.ToString("yyMMddHHmm");
                    AppLogger.LogInfo($"StockIn.AddToList: Auto-generated BatchNumber: {BatchNumber}");
                }
                else
                {
                    StatusMessage = "⚠ Batch number required."; 
                    AppLogger.LogWarning("StockIn.AddToList: Validation failed - BatchNumber is empty.");
                    return; 
                }
            }
            if (!ExpiryDate.HasValue) 
            { 
                if (IsAutoAddEnabled)
                {
                    ExpiryDate = DateTimeOffset.Now.AddYears(2);
                    AppLogger.LogInfo($"StockIn.AddToList: Set default ExpiryDate: {ExpiryDate}");
                }
                else
                {
                    StatusMessage = "⚠ Expiry date required."; 
                    AppLogger.LogWarning("StockIn.AddToList: Validation failed - ExpiryDate is empty.");
                    return; 
                }
            }
            if (QuantityUnits <= 0) 
            { 
                StatusMessage = "⚠ Quantity must be > 0."; 
                AppLogger.LogWarning($"StockIn.AddToList: Validation failed - QuantityUnits is {QuantityUnits}.");
                return; 
            }

            IsBusy = true;
            try
            {
                var med = FoundMedicine;
                if (med == null)
                {
                    AppLogger.LogInfo("StockIn.AddToList: FoundMedicine is null. Checking by name...");
                    var existing = await _medicineRepo.SearchAsync(EntryName);
                    med = existing.FirstOrDefault(m => 
                        m.Name.Equals(EntryName, StringComparison.OrdinalIgnoreCase) && 
                        (m.Strength ?? "").Equals(EntryDosage ?? "", StringComparison.OrdinalIgnoreCase));

                    if (med == null)
                    {
                        AppLogger.LogInfo($"StockIn.AddToList: Creating NEW medicine record for '{EntryName}'...");
                        med = new Medicine
                        {
                            Name = EntryName,
                            GenericName = string.Empty,
                            Strength = EntryDosage,
                            DosageForm = EntryDosage,
                            Barcode = string.IsNullOrWhiteSpace(BarcodeText) ? null : BarcodeText.Trim(),
                            CategoryName = "General",
                            ManufacturerName = "GSK",
                            GstPercent = EntryGst
                        };
                        med = await _medicineRepo.AddAsync(med);
                    }
                }

                if (med == null) 
                { 
                    StatusMessage = "✘ Error saving medicine record."; 
                    AppLogger.LogError("StockIn.AddToList: Medicine resolution failed.", null);
                    return; 
                }

                AppLogger.LogInfo($"StockIn.AddToList: Adding '{med.Name}' (ID: {med.Id}) to receiving list.");
                var item = new ReceivingItem
                {
                    MedicineId         = med.Id,
                    MedicineName       = med.Name,
                    ManufacturerName   = med.ManufacturerName ?? string.Empty,
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
                ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
                
                AppLogger.LogInfo($"StockIn.AddToList: Success. Clearing entry fields.");
                ClearEntry();
                RequestFocus?.Invoke(this, "MedicineName");
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Failed to add item.";
                AppLogger.LogError("StockIn.AddToList failed", ex);
            }
            finally { IsBusy = false; }
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

                _eventBus.Publish(InventoryChangeType.MedicineAdded);

                int total = ReceivingItems.Sum(r => r.QuantityUnits);
                StatusMessage = $"✔ Saved {batches.Count} batch(es) — {total} units added to inventory.";
                ReceivingItems.Clear();
                ((AsyncRelayCommand)SaveAllCommand).RaiseCanExecuteChanged();
                ClearEntry();

                SessionInvoiceNo = string.Empty;
                SessionSupplierName = string.Empty;
                SelectedSupplier = null;
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Unexpected error during save.";
                AppLogger.LogError("StockIn.SaveAll", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearEntry() => ClearEntryInternal(true);

        private void ClearEntryInternal(bool clearBarcode)
        {
            FoundMedicine = null;
            EntryName = string.Empty;
            EntryDosage = string.Empty;


            BatchNumber = string.Empty;
            ExpiryDate = null;
            QuantityUnitsText = "1";
            PurchaseTotalPriceText = "0";
            TotalSellingPriceText = "0";
            EntryGst = 0;
            OnPropertyChanged(nameof(EntryGstText));
            if (clearBarcode) BarcodeText = string.Empty;
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
