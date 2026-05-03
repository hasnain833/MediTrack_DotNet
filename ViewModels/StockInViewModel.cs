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
            SearchSuggestions = new ObservableCollection<Medicine>();

            SearchMedicineCommand = new AsyncRelayCommand(async _ => await SearchAsync());
            AddToListCommand = new RelayCommand(_ => AddToList());
            SaveAllCommand = new AsyncRelayCommand(async _ => await SaveInvoiceAsync(), _ => ReceivingItems.Count > 0);
            
            LoadSuppliers();
        }

        public ObservableCollection<ReceivingItem> ReceivingItems { get; }
        public ObservableCollection<Supplier> Suppliers { get; }
        public ObservableCollection<Medicine> SearchSuggestions { get; }

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

            // Create and add immediately to the list
            var newItem = new ReceivingItem
            {
                MedicineId = medicine.Id,
                MedicineName = medicine.Name,
                EntryMode = medicine.DefaultEntryMode,
                UnitsPerPack = medicine.UnitsPerPack,
                PacketsPerBox = medicine.PacketsPerBox,
                PackQuantity = 0,
                PackPrice = medicine.PurchasePrice, // Pre-fill with current saved price
                QuantityUnits = 0,
                PurchaseTotalPrice = 0,
                UnitCost = medicine.PurchasePrice / (medicine.UnitsPerPack > 0 ? medicine.UnitsPerPack : 1),
                SellingPricePerUnit = medicine.SellingPrice,
                ExpiryDate = medicine.ExpiryDate ?? DateTime.Now.AddYears(1)
            };

            // Insert at the top so it's easy to focus
            ReceivingItems.Insert(0, newItem);

            // Watch for changes in this row to update the Grand Total
            newItem.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(ReceivingItem.PurchaseTotalPrice))
                {
                    OnPropertyChanged(nameof(TotalSessionCost));
                }
            };
            
            // Clear search text to prepare for next search
            EntryName = string.Empty;
            OnPropertyChanged(nameof(EntryName));
        }

        private string _entryName = string.Empty;
        public string EntryName
        {
            get => _entryName;
            set
            {
                if (SetProperty(ref _entryName, value) && value.Length >= 2)
                    _ = SearchAsync();
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

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(EntryName) || EntryName.Length < 2) return;
            var results = await _medicineRepo.SearchAsync(EntryName);
            
            _dispatcher.TryEnqueue(() => {
                SearchSuggestions.Clear();
                foreach (var m in results) SearchSuggestions.Add(m);
            });
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
            OnPropertyChanged(nameof(CanSave));
            try
            {
                await _invoiceRepo.ProcessStockInAsync(
                    SessionSupplierName, 
                    SessionInvoiceNo, 
                    DateTime.ParseExact(SessionInvoiceDateText, "dd/MM/yyyy", CultureInfo.InvariantCulture),
                    ReceivingItems.ToList());

                ReceivingItems.Clear();
                _eventBus.Publish(InventoryChangeType.MedicineAdded);
            }
            catch (Exception ex) { AppLogger.LogError("StockIn save failed", ex); }
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
