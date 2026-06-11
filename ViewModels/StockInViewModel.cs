using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;
using Microsoft.UI.Dispatching;
using System.Globalization;
using System.Collections.Specialized;

namespace DChemist.ViewModels
{
    public class StockInViewModel : ViewModelBase
    {
        public enum QuantityInputMode { Box, Tablet }

        private readonly MedicineRepository _medicineRepo;
        private readonly SupplierRepository _supplierRepo;
        private readonly PurchaseInvoiceRepository _invoiceRepo;
        private readonly InventoryEventBus _eventBus;
        private readonly IDialogService _dialogService;
        private readonly DispatcherQueue _dispatcher;

        public StockInViewModel(
            MedicineRepository medicineRepo,
            SupplierRepository supplierRepo,
            PurchaseInvoiceRepository invoiceRepo,
            InventoryEventBus eventBus,
            IDialogService dialogService)
        {
            _medicineRepo = medicineRepo;
            _supplierRepo = supplierRepo;
            _invoiceRepo = invoiceRepo;
            _eventBus = eventBus;
            _dialogService = dialogService;
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            ReceivingItems = new ObservableCollection<ReceivingItem>();
            ReceivingItems.CollectionChanged += (s, e) => {
                OnPropertyChanged(nameof(TotalSessionCost));
                OnPropertyChanged(nameof(CanSave));
                (SaveAllCommand as AsyncRelayCommand)?.RaiseCanExecuteChanged();
            };

            Suppliers = new ObservableCollection<Supplier>();

            SearchMedicineCommand = new AsyncRelayCommand(async _ => await SearchAsync());
            AddToListCommand = new RelayCommand(_ => AddToList());
            SaveAllCommand = new AsyncRelayCommand(async _ => await SaveInvoiceAsync(), _ => ReceivingItems.Count > 0);
            
            _ = LoadSuppliers();
        }

        private readonly BulkObservableCollection<Medicine> _searchSuggestions = new();
        public ObservableCollection<ReceivingItem> ReceivingItems { get; }
        public ObservableCollection<Supplier> Suppliers { get; }
        public ObservableCollection<Medicine> SearchSuggestions => _searchSuggestions;

        public ICommand SearchMedicineCommand { get; }
        public ICommand AddToListCommand { get; }
        public ICommand SaveAllCommand { get; }

        private Medicine? _foundMedicine;
        public Medicine? FoundMedicine 
        { 
            get => _foundMedicine; 
            set 
            { 
                if (SetProperty(ref _foundMedicine, value)) 
                {
                    OnPropertyChanged(nameof(CanAddToList)); 
                    OnPropertyChanged(nameof(HasFoundMedicine));
                }
            }
        }

        public bool HasFoundMedicine => FoundMedicine != null;
        public string ActiveMedicineName => FoundMedicine?.Name ?? string.Empty;

        public string UnitCostDisplay => $"Cost: {UnitCost:N2}";
        public string SellingPriceDisplay => $"Sale: {FoundMedicine?.SellingPrice ?? 0:N2}";

        public void SelectMedicine(Medicine? medicine)
        {
            if (medicine == null) return;

            // Total tablets per box = packets/box × tablets/packet
            int totalUnitsPerBox = (medicine.PacketsPerBox > 0 ? medicine.PacketsPerBox : 1)
                                 * (medicine.UnitsPerPack  > 0 ? medicine.UnitsPerPack  : 1);

            // Pre-fill unit cost: purchase price was stored per-tablet in DB
            decimal prefillUnitCost = medicine.PurchasePrice > 0
                ? medicine.PurchasePrice                      // already per-tablet
                : medicine.SellingPrice / totalUnitsPerBox;   // fallback estimate

            var newItem = new ReceivingItem
            {
                MedicineId         = medicine.Id,
                MedicineName       = medicine.Name,
                BatchNo            = medicine.BatchNo ?? "Standard",
                EntryMode          = medicine.DefaultEntryMode,
                UnitsPerPack       = medicine.UnitsPerPack,
                PacketsPerBox      = medicine.PacketsPerBox,
                PackQuantity       = 0,
                PackPrice          = 0,
                QuantityUnits      = 0,
                PurchaseTotalPrice = 0,
                UnitCost           = prefillUnitCost,
                SellingPricePerUnit = medicine.SellingPrice,
                ExpiryDate         = medicine.ExpiryDate ?? DateTime.Now.AddYears(1)
            };

            ReceivingItems.Insert(0, newItem);

            newItem.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(ReceivingItem.PurchaseTotalPrice))
                    OnPropertyChanged(nameof(TotalSessionCost));
            };

            EntryName = string.Empty;
            OnPropertyChanged(nameof(EntryName));
        }

        private string _entryName = string.Empty;
        private CancellationTokenSource? _searchCts;
        public string EntryName
        {
            get => _entryName;
            set
            {
                if (SetProperty(ref _entryName, value) && value.Length >= 2)
                    _ = DebouncedSearchAsync();
            }
        }

        // Session Level
        private Supplier? _selectedSupplier;
        public Supplier? SelectedSupplier { get => _selectedSupplier; set => SetProperty(ref _selectedSupplier, value); }

        private string _sessionSupplierName = string.Empty;
        public string SessionSupplierName { get => _sessionSupplierName; set => SetProperty(ref _sessionSupplierName, value); }

        private string _sessionInvoiceNo = string.Empty;
        public string SessionInvoiceNo { get => _sessionInvoiceNo; set => SetProperty(ref _sessionInvoiceNo, value); }

        private string _sessionInvoiceDateText = DateTime.Now.ToString("dd/MM/yyyy");
        public string SessionInvoiceDateText { get => _sessionInvoiceDateText; set => SetProperty(ref _sessionInvoiceDateText, value); }

        // Entry Level
        private QuantityInputMode _selectedQuantityMode = QuantityInputMode.Box;
        public QuantityInputMode SelectedQuantityMode
        {
            get => _selectedQuantityMode;
            set { if (SetProperty(ref _selectedQuantityMode, value)) { OnPropertyChanged(nameof(IsBoxMode)); OnPropertyChanged(nameof(IsTabletMode)); RecalculateTotalUnits(); } }
        }

        public bool IsBoxMode => SelectedQuantityMode == QuantityInputMode.Box;
        public bool IsTabletMode => SelectedQuantityMode == QuantityInputMode.Tablet;

        private int _packQuantity;
        public int PackQuantity { get => _packQuantity; set { if (SetProperty(ref _packQuantity, value)) RecalculateTotalUnits(); } }

        private int _packetsPerBox = 1;
        public int PacketsPerBox { get => _packetsPerBox; set { if (SetProperty(ref _packetsPerBox, value)) RecalculateTotalUnits(); } }

        private int _unitsPerPacket = 1;
        public int UnitsPerPacket { get => _unitsPerPacket; set { if (SetProperty(ref _unitsPerPacket, value)) RecalculateTotalUnits(); } }

        private int _quantityUnits;
        public int QuantityUnits { get => _quantityUnits; set => SetProperty(ref _quantityUnits, value); }

        private decimal _purchaseTotalPrice;
        public decimal PurchaseTotalPrice { get => _purchaseTotalPrice; set { if (SetProperty(ref _purchaseTotalPrice, value)) OnPropertyChanged(nameof(UnitCost)); } }

        public decimal UnitCost => QuantityUnits > 0 ? PurchaseTotalPrice / QuantityUnits : 0;

        public decimal TotalSessionCost => ReceivingItems.Sum(i => i.PurchaseTotalPrice);
        public bool CanSave => ReceivingItems.Count > 0 && !IsBusy;

        public string PackQuantityText
        {
            get => _packQuantity == 0 ? string.Empty : _packQuantity.ToString();
            set { if (int.TryParse(value, out int res)) PackQuantity = res; else PackQuantity = 0; OnPropertyChanged(nameof(PackQuantityText)); }
        }

        public string PacketsPerBoxText
        {
            get => _packetsPerBox == 0 ? string.Empty : _packetsPerBox.ToString();
            set { if (int.TryParse(value, out int res)) PacketsPerBox = res; else PacketsPerBox = 1; OnPropertyChanged(nameof(PacketsPerBoxText)); }
        }

        public string UnitsPerPacketText
        {
            get => _unitsPerPacket == 0 ? string.Empty : _unitsPerPacket.ToString();
            set { if (int.TryParse(value, out int res)) UnitsPerPacket = res; else UnitsPerPacket = 1; OnPropertyChanged(nameof(UnitsPerPacketText)); }
        }

        public string QuantityUnitsText
        {
            get => _quantityUnits == 0 ? string.Empty : _quantityUnits.ToString();
            set { if (int.TryParse(value, out int res)) QuantityUnits = res; else QuantityUnits = 0; OnPropertyChanged(nameof(QuantityUnitsText)); }
        }

        public string PurchaseTotalPriceText
        {
            get => _purchaseTotalPrice == 0 ? string.Empty : _purchaseTotalPrice.ToString("N2");
            set { if (decimal.TryParse(value, out decimal res)) PurchaseTotalPrice = res; else PurchaseTotalPrice = 0; OnPropertyChanged(nameof(PurchaseTotalPriceText)); }
        }

        private void RecalculateTotalUnits()
        {
            if (IsBoxMode)
                QuantityUnits = PackQuantity * PacketsPerBox * UnitsPerPacket;
            
            OnPropertyChanged(nameof(QuantityUnitsText));
            OnPropertyChanged(nameof(UnitCost));
            OnPropertyChanged(nameof(UnitCostDisplay));
        }

        public bool CanAddToList => FoundMedicine != null;

        private void AddToList()
        {
            if (FoundMedicine == null) return;

            var item = new ReceivingItem
            {
                MedicineId = FoundMedicine.Id,
                MedicineName = FoundMedicine.Name,
                BatchNo = FoundMedicine.BatchNo ?? "Standard",
                QuantityUnits = QuantityUnits,
                PurchaseTotalPrice = PurchaseTotalPrice,
                UnitCost = UnitCost,
                SellingPricePerUnit = FoundMedicine.SellingPrice,
                EntryMode = SelectedQuantityMode.ToString(),
                UnitsPerPack = UnitsPerPacket,
                PackQuantity = PackQuantity
            };

            ReceivingItems.Add(item);
            ClearEntry();
        }

        public void ClearEntry()
        {
            EntryName = string.Empty;
            FoundMedicine = null;
            PackQuantityText = string.Empty;
            QuantityUnitsText = string.Empty;
            PurchaseTotalPriceText = string.Empty;
            OnPropertyChanged(nameof(CanAddToList));
        }

        private async Task DebouncedSearchAsync()
        {
            _searchCts?.Cancel();

            if (string.IsNullOrWhiteSpace(EntryName) || EntryName.Length < 2) return;

            var cts = new CancellationTokenSource();
            _searchCts = cts;

            try
            {
                await Task.Delay(300, cts.Token);
            }
            catch (TaskCanceledException) { return; }

            if (cts.Token.IsCancellationRequested) return;

            await SearchAsync();
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(EntryName) || EntryName.Length < 2) return;
            var results = await _medicineRepo.SearchAsync(EntryName);
            
            _dispatcher.TryEnqueue(() => _searchSuggestions.ReplaceAll(results));
        }

        private async Task LoadSuppliers()
        {
            var list = await _supplierRepo.GetAllAsync();
            Suppliers.Clear();
            foreach (var s in list) Suppliers.Add(s);
        }

        private async Task SaveInvoiceAsync()
        {
            if (ReceivingItems.Count == 0) return;
            IsBusy = true;
            StatusMessage = string.Empty;
            OnPropertyChanged(nameof(CanSave));

            // Pre-save validation
            var invalidItem = ReceivingItems.FirstOrDefault(i => i.QuantityUnits <= 0 || i.PurchaseTotalPrice <= 0 || string.IsNullOrWhiteSpace(i.BatchNo));
            if (invalidItem != null)
            {
                StatusMessage = $"⚠ Validation failed: '{invalidItem.MedicineName}' has missing quantity, price, or batch number.";
                IsBusy = false;
                OnPropertyChanged(nameof(CanSave));
                return;
            }

            try
            {
                // Auto-generate a unique invoice number if blank or still the default
                string invoiceNo = SessionInvoiceNo?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(invoiceNo) || invoiceNo == "INV-000")
                    invoiceNo = "INV-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");

                if (!DateTime.TryParseExact(SessionInvoiceDateText, "dd/MM/yyyy",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out DateTime invoiceDate))
                {
                    invoiceDate = DateTime.Today;
                }

                await _invoiceRepo.ProcessStockInAsync(
                    SessionSupplierName,
                    invoiceNo,
                    invoiceDate,
                    ReceivingItems.ToList());

                ReceivingItems.Clear();
                SessionInvoiceNo = string.Empty;  // reset so next save also auto-generates
                StatusMessage = "✔ Purchase saved successfully.";
                _eventBus.Publish(InventoryChangeType.MedicineAdded);
            }
            catch (Exception ex)
            {
                StatusMessage = "⚠ Could not save purchase. Please try again.";
                AppLogger.LogError("StockIn save failed", ex);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanSave));
            }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        private bool _showPackagingDetails;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public bool ShowPackagingDetails { get => _showPackagingDetails; set => SetProperty(ref _showPackagingDetails, value); }
    }
}
