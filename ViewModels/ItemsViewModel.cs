using System;
using System.Collections.ObjectModel;
using System.Globalization;
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
    public class ItemsViewModel : ViewModelBase
    {
        public enum QuantityInputMode { Box, Tablet }
        public event EventHandler<string>? RequestFocus;
        private readonly MedicineRepository _medicineRepo;
        private readonly BatchRepository _batchRepo;
        private readonly InventoryEventBus _eventBus;
        private readonly IDialogService _dialogService;
        private readonly DispatcherQueue _dispatcher;

        private readonly AuthorizationService _auth;
        private readonly IReportingService _reportingService;
        private string _searchText = string.Empty;

        public ItemsViewModel(
            MedicineRepository medicineRepo,
            BatchRepository batchRepo,
            AuthorizationService auth,
            InventoryEventBus eventBus,
            IReportingService reportingService,
            IDialogService dialogService)
        {
            _medicineRepo = medicineRepo;
            _batchRepo = batchRepo;
            _auth = auth;
            _eventBus = eventBus;
            _reportingService = reportingService;
            _dialogService = dialogService;
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            Medicines = new ObservableCollection<Medicine>();
            
            RefreshCommand = new AsyncRelayCommand(async _ => await RefreshAsync());
            DeleteMedicineCommand = new AsyncRelayCommand(async m => await ExecuteDeleteMedicineAsync(m as Medicine));
            DeleteBatchCommand = new AsyncRelayCommand(async m => await ExecuteDeleteBatchAsync(m as Medicine));
            BeginEditCommand = new RelayCommand(m => (m as Medicine)?.BeginEdit());
            CancelEditCommand = new RelayCommand(m => (m as Medicine)?.CancelEdit());
            SaveRowCommand = new AsyncRelayCommand(async m => await ExecuteSaveRowAsync(m as Medicine));
            EditInFormCommand = new RelayCommand(m => ExecuteEditInForm(m as Medicine));
            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportInventoryToCsvAsync(Medicines));

            LookupBarcodeCommand = new AsyncRelayCommand(async _ => await ExecuteLookupBarcodeAsync());
            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSaveAsync());
            ClearEntryCommand = new RelayCommand(_ => ClearEntry());

            _eventBus.InventoryChanged += OnInventoryChanged;
            
            // Initial load
            _ = RefreshAsync();
        }

        public Task LoadAsync() => RefreshAsync();
        public ObservableCollection<Medicine> Medicines { get; }
        public bool IsAdmin => _auth.IsAdmin;

        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = SearchAsync(); }
        }

        public ICommand RefreshCommand { get; }
        public ICommand DeleteMedicineCommand { get; }
        public ICommand DeleteBatchCommand { get; }
        public ICommand BeginEditCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand SaveRowCommand { get; }
        public ICommand EditInFormCommand { get; }
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

        private async Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                var list = await _medicineRepo.GetAllAsync();
                Medicines.Clear();
                foreach (var item in list)
                {
                    Medicines.Add(item);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Failed to load inventory.";
                AppLogger.LogError("ItemsViewModel.Refresh", ex);
            }
            finally { IsBusy = false; }
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText)) { await RefreshAsync(); return; }

            IsBusy = true;
            try
            {
                var list = await _medicineRepo.SearchAsync(SearchText);
                Medicines.Clear();
                foreach (var item in list)
                {
                    Medicines.Add(item);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Search failed.";
                AppLogger.LogError("ItemsViewModel.Search", ex);
            }
            finally { IsBusy = false; }
        }

        public bool IsBoxMode => SelectedQuantityMode == QuantityInputMode.Box;
        public bool IsTabletMode => SelectedQuantityMode == QuantityInputMode.Tablet;

        private QuantityInputMode _selectedQuantityMode = QuantityInputMode.Box;
        public QuantityInputMode SelectedQuantityMode
        {
            get => _selectedQuantityMode;
            set
            {
                if (SetProperty(ref _selectedQuantityMode, value))
                {
                    OnPropertyChanged(nameof(IsBoxMode));
                    OnPropertyChanged(nameof(IsTabletMode));
                    RecalculateTotalUnits();
                }
            }
        }

        private string _barcodeText = string.Empty;
        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }

        private string _entryName = string.Empty;
        public string EntryName { get => _entryName; set => SetProperty(ref _entryName, value); }

        private string _batchNumber = string.Empty;
        public string BatchNumber { get => _batchNumber; set => SetProperty(ref _batchNumber, value); }

        private DateTimeOffset? _expiryDate;
        public DateTimeOffset? ExpiryDate
        {
            get => _expiryDate;
            set
            {
                if (SetProperty(ref _expiryDate, value))
                {
                    OnPropertyChanged(nameof(ExpiryDateText));
                }
            }
        }

        private string _expiryDateText = string.Empty;
        public string ExpiryDateText
        {
            get => _expiryDateText;
            set => SetProperty(ref _expiryDateText, value);
        }

        private int _packetsPerBox = 0;
        public int PacketsPerBox
        {
            get => _packetsPerBox;
            set { if (SetProperty(ref _packetsPerBox, value)) { OnPropertyChanged(nameof(PacketsPerBoxText)); RecalculateTotalUnits(); } }
        }

        private int _unitsPerPacket = 0;
        public int UnitsPerPacket
        {
            get => _unitsPerPacket;
            set { if (SetProperty(ref _unitsPerPacket, value)) { OnPropertyChanged(nameof(UnitsPerPacketText)); RecalculateTotalUnits(); } }
        }

        private int _packQuantity = 0;
        public int PackQuantity
        {
            get => _packQuantity;
            set { if (SetProperty(ref _packQuantity, value)) { OnPropertyChanged(nameof(PackQuantityText)); RecalculateTotalUnits(); } }
        }

        private int _quantityUnits = 0;
        public int QuantityUnits
        {
            get => _quantityUnits;
            set
            {
                if (SetProperty(ref _quantityUnits, value))
                {
                    OnPropertyChanged(nameof(QuantityUnitsText));
                    OnPropertyChanged(nameof(TotalUnitsPreviewText));
                }
            }
        }

        private decimal _sellingPrice = 0;
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set { if (SetProperty(ref _sellingPrice, value)) OnPropertyChanged(nameof(SellingPriceText)); }
        }

        public string PacketsPerBoxText
        {
            get => _packetsPerBox == 0 ? string.Empty : _packetsPerBox.ToString(CultureInfo.InvariantCulture);
            set { if (string.IsNullOrWhiteSpace(value)) PacketsPerBox = 0; else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res)) PacketsPerBox = res; OnPropertyChanged(nameof(PacketsPerBoxText)); }
        }

        public string UnitsPerPacketText
        {
            get => _unitsPerPacket == 0 ? string.Empty : _unitsPerPacket.ToString(CultureInfo.InvariantCulture);
            set { if (string.IsNullOrWhiteSpace(value)) UnitsPerPacket = 0; else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res)) UnitsPerPacket = res; OnPropertyChanged(nameof(UnitsPerPacketText)); }
        }

        public string PackQuantityText
        {
            get => _packQuantity == 0 ? string.Empty : _packQuantity.ToString(CultureInfo.InvariantCulture);
            set { if (string.IsNullOrWhiteSpace(value)) PackQuantity = 0; else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res)) PackQuantity = res; OnPropertyChanged(nameof(PackQuantityText)); }
        }

        public string QuantityUnitsText
        {
            get => _quantityUnits == 0 ? string.Empty : _quantityUnits.ToString(CultureInfo.InvariantCulture);
            set { if (string.IsNullOrWhiteSpace(value)) QuantityUnits = 0; else if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int res)) QuantityUnits = res; OnPropertyChanged(nameof(QuantityUnitsText)); }
        }

        public string SellingPriceText
        {
            get => _sellingPrice == 0 ? string.Empty : _sellingPrice.ToString("G29", CultureInfo.InvariantCulture);
            set { if (string.IsNullOrWhiteSpace(value)) SellingPrice = 0; else if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out decimal res)) SellingPrice = res; OnPropertyChanged(nameof(SellingPriceText)); }
        }

        public string TotalUnitsPreviewText
        {
            get
            {
                if (IsTabletMode) return string.Empty;
                return $"({PackQuantity} box × {PacketsPerBox} pack × {UnitsPerPacket} tab = {QuantityUnits} tabs)";
            }
        }

        public ICommand LookupBarcodeCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearEntryCommand { get; }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _isFormExpanded = false;
        public bool IsFormExpanded { get => _isFormExpanded; set => SetProperty(ref _isFormExpanded, value); }

        // Edit mode flag - shows extra fields (Category, Purchase Price, Stock Qty) only when editing
        private bool _isEditMode;
        public bool IsEditMode { get => _isEditMode; set => SetProperty(ref _isEditMode, value); }

        // Edit-only extra fields
        private string _editCategory = string.Empty;
        public string EditCategory { get => _editCategory; set => SetProperty(ref _editCategory, value); }

        private string _editPurchasePriceText = string.Empty;
        public string EditPurchasePriceText
        {
            get => _editPurchasePriceText;
            set => SetProperty(ref _editPurchasePriceText, value);
        }

        private string _editStockQtyText = string.Empty;
        public string EditStockQtyText
        {
            get => _editStockQtyText;
            set => SetProperty(ref _editStockQtyText, value);
        }

        private Medicine? _foundMedicine;

        private void RecalculateTotalUnits()
        {
            if (SelectedQuantityMode == QuantityInputMode.Tablet) return;
            QuantityUnits = Math.Max(0, PackQuantity * PacketsPerBox * UnitsPerPacket);
        }

        public void FormatExpiryDate()
        {
            if (string.IsNullOrWhiteSpace(ExpiryDateText)) return;
            string input = new string(ExpiryDateText.Where(char.IsDigit).ToArray());
            DateTimeOffset? result = null;
            try
            {
                if (input.Length == 4)
                {
                    int month = int.Parse(input.Substring(0, 2));
                    int year = int.Parse("20" + input.Substring(2, 2));
                    result = new DateTimeOffset(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
                }
                else if (input.Length == 6)
                {
                    int month = int.Parse(input.Substring(0, 2));
                    int year = int.Parse(input.Substring(2, 4));
                    result = new DateTimeOffset(new DateTime(year, month, DateTime.DaysInMonth(year, month)));
                }
                else if (input.Length == 8)
                {
                    int day = int.Parse(input.Substring(0, 2));
                    int month = int.Parse(input.Substring(2, 2));
                    int year = int.Parse(input.Substring(4, 4));
                    result = new DateTimeOffset(new DateTime(year, month, day));
                }

                if (result.HasValue)
                {
                    ExpiryDate = result;
                    _expiryDateText = input.Length == 8 ? result.Value.ToString("dd/MM/yyyy") : result.Value.ToString("MM/yyyy");
                    OnPropertyChanged(nameof(ExpiryDateText));
                }
            }
            catch { }
        }

        private async Task ExecuteLookupBarcodeAsync()
        {
            var barcode = BarcodeText.Trim();
            if (string.IsNullOrEmpty(barcode)) return;

            try
            {
                var medicine = await _medicineRepo.GetByBarcodeAsync(barcode);
                if (medicine != null)
                {
                    _foundMedicine = medicine;
                    EntryName = medicine.Name;
                    StatusMessage = $"✔ Found: {medicine.Name}";
                    RequestFocus?.Invoke(this, "BatchNumber");
                }
                else
                {
                    StatusMessage = $"ℹ New Barcode: {barcode}";
                    RequestFocus?.Invoke(this, "MedicineName");
                }
            }
            catch (Exception ex) { StatusMessage = "✘ Lookup failed."; AppLogger.LogError("Items.Lookup", ex); }
        }

        private async Task ExecuteSaveAsync()
        {
            FormatExpiryDate();
            if (string.IsNullOrWhiteSpace(EntryName)) { StatusMessage = "⚠ Medicine name required."; return; }
            
            IsBusy = true;
            try
            {
                int totalUnitsPerBox = (PacketsPerBox > 0 ? PacketsPerBox : 1) * (UnitsPerPacket > 0 ? UnitsPerPacket : 1);
                decimal sellingPricePerUnit = SelectedQuantityMode == QuantityInputMode.Box
                    ? SellingPrice / totalUnitsPerBox
                    : SellingPrice;

                var med = _foundMedicine;
                
                // 1. Update/Add Medicine Metadata
                if (med != null)
                {
                    med.Name = EntryName;
                    med.Barcode = string.IsNullOrWhiteSpace(BarcodeText) ? null : BarcodeText.Trim();
                    med.UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1;
                    med.PacketsPerBox = PacketsPerBox > 0 ? PacketsPerBox : 1;
                    med.DefaultEntryMode = SelectedQuantityMode.ToString();

                    // Apply edit-mode extra fields if in edit mode
                    if (IsEditMode)
                    {
                        if (!string.IsNullOrWhiteSpace(EditCategory))
                            med.CategoryName = EditCategory;
                        if (decimal.TryParse(EditPurchasePriceText, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out decimal pp))
                            med.PurchasePrice = pp;
                        if (int.TryParse(EditStockQtyText, out int sq))
                            med.StockQty = sq;
                    }

                    await _medicineRepo.UpdateAsync(med);
                }
                else
                {
                    // Check if it already exists by name
                    var existing = await _medicineRepo.SearchAsync(EntryName);
                    med = existing.FirstOrDefault(m => m.Name.Equals(EntryName, StringComparison.OrdinalIgnoreCase));

                    if (med == null)
                    {
                        med = new Medicine 
                        { 
                            Name = EntryName, 
                            Barcode = string.IsNullOrWhiteSpace(BarcodeText) ? null : BarcodeText.Trim(), 
                            CategoryName = "General", 
                            ManufacturerName = "General",
                            DefaultEntryMode = SelectedQuantityMode.ToString(),
                            UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1,
                            PacketsPerBox = PacketsPerBox > 0 ? PacketsPerBox : 1
                        };
                        med = await _medicineRepo.AddAsync(med);
                    }
                    else
                    {
                        med.DefaultEntryMode = SelectedQuantityMode.ToString();
                        med.UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1;
                        med.PacketsPerBox = PacketsPerBox > 0 ? PacketsPerBox : 1;
                        await _medicineRepo.UpdateAsync(med);
                    }
                }

                // 2. Update/Add Batch
                if (med.BatchId.HasValue)
                {
                    // Update existing batch
                    var batch = await _batchRepo.GetByIdAsync(med.BatchId.Value);
                    if (batch != null)
                    {
                        batch.BatchNo = string.IsNullOrWhiteSpace(BatchNumber) ? batch.BatchNo : BatchNumber;
                        batch.SellingPrice = sellingPricePerUnit;
                        batch.ExpiryDate = ExpiryDate?.DateTime ?? batch.ExpiryDate;
                        batch.UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1;

                        // Apply purchase price and stock qty if editing
                        if (IsEditMode)
                        {
                            if (decimal.TryParse(EditPurchasePriceText, System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture, out decimal pp))
                                batch.UnitCost = pp;
                            if (int.TryParse(EditStockQtyText, out int sq))
                            {
                                batch.QuantityUnits = sq;
                                batch.RemainingUnits = sq;
                            }
                        }

                        await _batchRepo.UpdateAsync(batch);
                    }
                }
                else
                {
                    // Create new batch
                    var batch = new InventoryBatch
                    {
                        MedicineId = med.Id,
                        BatchNo = string.IsNullOrWhiteSpace(BatchNumber) ? "B-" + DateTime.Now.ToString("yyMMdd") : BatchNumber,
                        QuantityUnits = 0,
                        RemainingUnits = 0,
                        SellingPrice = sellingPricePerUnit,
                        ExpiryDate = ExpiryDate?.DateTime ?? DateTime.Now.AddYears(1),
                        EntryMode = SelectedQuantityMode.ToString(),
                        UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1,
                        PackQuantity = PackQuantity
                    };
                    await _batchRepo.AddAsync(batch);
                }
                
                _eventBus.Publish(InventoryChangeType.MedicineAdded);
                _ = RefreshAsync();

                StatusMessage = $"✔ Saved: {med.Name}";
                ClearEntry();
                RequestFocus?.Invoke(this, "Barcode");
            }
            catch (Exception ex) 
            { 
                StatusMessage = "✘ Save failed."; 
                AppLogger.LogError("Items.Save", ex); 
            }
            finally { IsBusy = false; }
        }

        private void ExecuteEditInForm(Medicine? medicine)
        {
            if (medicine == null) return;
            
            _foundMedicine = medicine;
            BarcodeText = medicine.Barcode ?? string.Empty;
            EntryName = medicine.Name;
            BatchNumber = medicine.BatchNo ?? string.Empty;
            
            if (medicine.ExpiryDate.HasValue)
            {
                ExpiryDate = new DateTimeOffset(medicine.ExpiryDate.Value);
                ExpiryDateText = medicine.ExpiryDate.Value.ToString("MM/yyyy");
            }
            else
            {
                ExpiryDate = null;
                ExpiryDateText = string.Empty;
            }

            PacketsPerBox = medicine.PacketsPerBox;
            UnitsPerPacket = medicine.UnitsPerPack;
            
            // For price, if it's stored as price-per-tablet, we might want to convert it back to price-per-box if that's the default
            bool isBox = medicine.DefaultEntryMode == "Box";
            SelectedQuantityMode = isBox ? QuantityInputMode.Box : QuantityInputMode.Tablet;
            
            if (isBox)
            {
                int totalUnits = Math.Max(1, medicine.PacketsPerBox * medicine.UnitsPerPack);
                SellingPrice = medicine.SellingPrice * totalUnits;
            }
            else
            {
                SellingPrice = medicine.SellingPrice;
            }

            // Populate edit-only extra fields
            EditCategory = medicine.CategoryName ?? string.Empty;
            EditPurchasePriceText = medicine.PurchasePrice > 0
                ? medicine.PurchasePrice.ToString("G29", System.Globalization.CultureInfo.InvariantCulture)
                : string.Empty;
            EditStockQtyText = medicine.StockQty > 0 ? medicine.StockQty.ToString() : string.Empty;

            // Activate edit mode so extra fields become visible
            IsEditMode = true;
            IsFormExpanded = true;
            StatusMessage = $"Editing: {medicine.Name}";
            
            OnPropertyChanged(nameof(SellingPriceText));
            OnPropertyChanged(nameof(ExpiryDateText));
            OnPropertyChanged(nameof(PacketsPerBoxText));
            OnPropertyChanged(nameof(UnitsPerPacketText));
        }

        private async Task ExecuteDeleteMedicineAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            try
            {
                await _medicineRepo.DeleteAsync(medicine.Id);
                Medicines.Remove(medicine);
                StatusMessage = $"✔ Deleted: {medicine.Name}";
            }
            catch (DataAccessException ex)
            {
                AppLogger.LogError("Items.DeleteMed", ex);
                await _dialogService.ShowMessageAsync("Cannot Delete Medicine", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Items.DeleteMed", ex);
                await _dialogService.ShowMessageAsync("Delete Failed", $"An unexpected error occurred:\n{ex.Message}", "OK");
            }
        }

        private async Task ExecuteDeleteBatchAsync(Medicine? medicine)
        {
            if (medicine == null || !medicine.BatchId.HasValue) return;
            bool confirmed = await _dialogService.ShowConfirmationAsync("Delete Batch", $"Are you sure you want to delete Batch {medicine.BatchNo} for {medicine.Name}?", "Delete", "Cancel");
            if (confirmed)
            {
                try { await _batchRepo.DeleteAsync(medicine.BatchId.Value); Medicines.Remove(medicine); _eventBus.Publish(InventoryChangeType.MedicineDeleted); }
                catch (Exception ex) { StatusMessage = "✘ Delete failed."; AppLogger.LogError("Items.DeleteBatch", ex); }
            }
        }

        private async Task ExecuteSaveRowAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            medicine.CommitEdit();
            try { await _medicineRepo.UpdateMetadataAsync(medicine); StatusMessage = "✔ Updated successfully."; }
            catch (Exception ex) { StatusMessage = "✘ Update failed."; AppLogger.LogError("Items.UpdateRow", ex); medicine.BeginEdit(); }
        }

        private void ClearEntry()
        {
            _foundMedicine = null;
            BarcodeText = string.Empty;
            EntryName = string.Empty;
            BatchNumber = string.Empty;
            ExpiryDate = null;
            ExpiryDateText = string.Empty;
            PackQuantity = 0;
            PacketsPerBox = 0;
            UnitsPerPacket = 0;
            QuantityUnits = 0;
            SellingPrice = 0;
            // Reset edit-mode extra fields and revert to normal entry mode
            IsEditMode = false;
            EditCategory = string.Empty;
            EditPurchasePriceText = string.Empty;
            EditStockQtyText = string.Empty;
            OnPropertyChanged(nameof(PackQuantityText));
            OnPropertyChanged(nameof(PacketsPerBoxText));
            OnPropertyChanged(nameof(UnitsPerPacketText));
            OnPropertyChanged(nameof(QuantityUnitsText));
            OnPropertyChanged(nameof(SellingPriceText));
        }
    }
}
