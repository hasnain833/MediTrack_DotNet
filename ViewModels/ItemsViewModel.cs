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

        public ItemsViewModel(
            MedicineRepository medicineRepo,
            BatchRepository batchRepo,
            InventoryEventBus eventBus,
            IDialogService dialogService)
        {
            _medicineRepo = medicineRepo;
            _batchRepo = batchRepo;
            _eventBus = eventBus;
            _dialogService = dialogService;
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            AddedItems = new ObservableCollection<ReceivingItem>();
            LookupBarcodeCommand = new AsyncRelayCommand(async _ => await ExecuteLookupBarcodeAsync());
            SaveCommand = new AsyncRelayCommand(async _ => await ExecuteSaveAsync());
            ClearEntryCommand = new RelayCommand(_ => ClearEntry());
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

        public ObservableCollection<ReceivingItem> AddedItems { get; }
        public ICommand LookupBarcodeCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearEntryCommand { get; }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private bool _isFormExpanded = true;
        public bool IsFormExpanded { get => _isFormExpanded; set => SetProperty(ref _isFormExpanded, value); }

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
            if (SellingPrice <= 0) { StatusMessage = "⚠ Selling price required."; return; }
            int totalUnitsPerBox = (PacketsPerBox > 0 ? PacketsPerBox : 1) * (UnitsPerPacket > 0 ? UnitsPerPacket : 1);
            decimal sellingPricePerUnit = SelectedQuantityMode == QuantityInputMode.Box
                ? SellingPrice / totalUnitsPerBox
                : SellingPrice;

            IsBusy = true;
            try
            {
                var med = _foundMedicine;
                if (med == null)
                {
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
                    else if (SelectedQuantityMode == QuantityInputMode.Box)
                    {
                        med.DefaultEntryMode = "Box";
                        med.UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1;
                        med.PacketsPerBox = PacketsPerBox > 0 ? PacketsPerBox : 1;
                        await _medicineRepo.UpdateAsync(med);
                    }
                }

                var batch = new InventoryBatch
                {
                    MedicineId = med.Id,
                    BatchNo = string.IsNullOrWhiteSpace(BatchNumber) ? "B-" + DateTime.Now.ToString("yyMMdd") : BatchNumber,
                    QuantityUnits = 0,          // Item page saves packaging only — stock starts at 0
                    RemainingUnits = 0,
                    SellingPrice = sellingPricePerUnit,  // stored as price-per-tablet
                    ExpiryDate = ExpiryDate?.DateTime ?? DateTime.Now.AddYears(1),
                    EntryMode = SelectedQuantityMode.ToString(),
                    UnitsPerPack = UnitsPerPacket > 0 ? UnitsPerPacket : 1,
                    PackQuantity = PackQuantity
                };
                
                await _batchRepo.AddAsync(batch);
                _eventBus.Publish(InventoryChangeType.MedicineAdded);

                var item = new ReceivingItem
                {
                    MedicineName = med.Name,
                    BatchNo = batch.BatchNo,
                    QuantityUnits = batch.QuantityUnits,
                    TotalSellingPrice = SellingPrice,
                    ExpiryDate = batch.ExpiryDate
                };
                AddedItems.Insert(0, item);

                StatusMessage = $"✔ Saved: {med.Name}";
                ClearEntry();
                RequestFocus?.Invoke(this, "Barcode");
            }
            catch (Exception ex) { StatusMessage = "✘ Save failed."; AppLogger.LogError("Items.Save", ex); }
            finally { IsBusy = false; }
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
            OnPropertyChanged(nameof(PackQuantityText));
            OnPropertyChanged(nameof(PacketsPerBoxText));
            OnPropertyChanged(nameof(UnitsPerPacketText));
            OnPropertyChanged(nameof(QuantityUnitsText));
            OnPropertyChanged(nameof(SellingPriceText));
        }
    }
}
