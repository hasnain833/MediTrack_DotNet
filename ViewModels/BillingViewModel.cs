using System;
using System.Globalization;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Models.UseCases;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class BillingViewModel : ViewModelBase
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
        private readonly MedicineRepository _medicineRepository;
        private readonly BatchRepository _batchRepository;
        private readonly SettingsService _settingsService;
        private readonly ISalesWorkflowService _salesWorkflow;

        private decimal _taxRate;
        private string _searchMedicineText = string.Empty;
        private string _customerName = string.Empty;
        private string _customerPhone = string.Empty;
        private decimal _totalAmount;
        private decimal _taxAmount;
        private decimal _discountAmount;
        private decimal _discountRupees;
        private string _discountText = string.Empty;
        private string _discountPercentText = string.Empty;
        private decimal _discountPercent;
        private bool _usePercentageDiscount;
        private decimal _grandTotal;
        private Medicine? _selectedMedicine;
        private string _barcodeText = string.Empty;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private bool _isStatusSuccess;
        private bool _isContinuousScanMode = true;
        private bool _isSearching;
        private CancellationTokenSource? _searchCts;

        public bool IsSearching { get => _isSearching; set => SetProperty(ref _isSearching, value); }

        public BillingViewModel(
            MedicineRepository medicineRepository,
            BatchRepository batchRepository,
            SettingsService settingsService,
            ISalesWorkflowService salesWorkflow)
        {
            _medicineRepository = medicineRepository;
            _batchRepository = batchRepository;
            _settingsService = settingsService;
            _salesWorkflow = salesWorkflow;
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            _taxRate = 0.0m;

            CartItems = new ObservableCollection<SaleItemViewModel>();

            SearchCommand = new AsyncRelayCommand(async _ => await SearchMedicinesAsync());
            AddToCartCommand = new AsyncRelayCommand(async _ => await ExecuteAddToCartAsync(), _ => SelectedMedicine != null);
            RemoveFromCartCommand = new RelayCommand(item => ExecuteRemoveFromCart(item as SaleItemViewModel), item => item is SaleItemViewModel);
            CompleteSaleReportedCommand = new AsyncRelayCommand(async _ => await ExecuteCompleteSaleAsync(true), _ => CartItems.Any() && !IsBusy);
            CompleteSaleInternalCommand = new AsyncRelayCommand(async _ => await ExecuteCompleteSaleAsync(false), _ => CartItems.Any() && !IsBusy);
            PrintBillCommand = new AsyncRelayCommand(async _ => await ExecutePrintBillAsync());
            ClearCartCommand = new RelayCommand(_ => ExecuteClearCart(), _ => CartItems.Any());
        }

        public async Task InitializeAsync()
        {
            _taxRate = await _settingsService.GetTaxRateAsync();
            OnPropertyChanged(nameof(TaxRateText));
            UpdateTotals();
        }

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsStatusSuccess { get => _isStatusSuccess; set => SetProperty(ref _isStatusSuccess, value); }

        private readonly BulkObservableCollection<Medicine> _medicineResults = new();
        public ObservableCollection<SaleItemViewModel> CartItems { get; }
        public ObservableCollection<Medicine> MedicineResults => _medicineResults;

        public string SearchMedicineText
        {
            get => _searchMedicineText;
            set { if (SetProperty(ref _searchMedicineText, value)) _ = DebouncedSearchAsync(); }
        }

        public Medicine? SelectedMedicine
        {
            get => _selectedMedicine;
            set { if (SetProperty(ref _selectedMedicine, value)) ((AsyncRelayCommand)AddToCartCommand).RaiseCanExecuteChanged(); }
        }

        public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
        public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }
        public decimal TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }
        public decimal TaxAmount { get => _taxAmount; set => SetProperty(ref _taxAmount, value); }
        public string TaxRateText => $"Tax ({_taxRate * 100:0.##}%)";
        public decimal DiscountAmount { get => _discountAmount; private set => SetProperty(ref _discountAmount, value); }
        public string DiscountText
        {
            get => _discountText;
            set
            {
                if (!SetProperty(ref _discountText, value)) return;
                _usePercentageDiscount = false;
                _discountRupees = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0;
                _discountPercent = 0;
                _discountPercentText = string.Empty;
                OnPropertyChanged(nameof(DiscountPercentText));
                UpdateTotals();
            }
        }
        public string DiscountPercentText
        {
            get => _discountPercentText;
            set
            {
                if (!SetProperty(ref _discountPercentText, value)) return;
                _usePercentageDiscount = true;
                _discountPercent = decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) ? percent : 0;
                _discountRupees = 0;
                _discountText = string.Empty;
                OnPropertyChanged(nameof(DiscountText));
                UpdateTotals();
            }
        }
        public decimal GrandTotal { get => _grandTotal; set => SetProperty(ref _grandTotal, value); }

        public string ItemCountText => CartItems.Count == 1 ? "1 item" : $"{CartItems.Count} items";

        // Cash handed over by the customer → change to return. Display only; not saved with the sale.
        private string _cashReceivedText = string.Empty;
        public string CashReceivedText
        {
            get => _cashReceivedText;
            set
            {
                if (SetProperty(ref _cashReceivedText, value))
                {
                    OnPropertyChanged(nameof(ChangeText));
                    OnPropertyChanged(nameof(IsChangeShort));
                    OnPropertyChanged(nameof(IsChangeOk));
                }
            }
        }
        private decimal? CashReceived => decimal.TryParse(CashReceivedText.Replace(",", "").Trim(), out var c) && c > 0 ? c : null;
        public bool IsChangeShort => CashReceived is decimal c && c < Math.Round(GrandTotal);
        public bool IsChangeOk => !IsChangeShort;
        public string ChangeText => CashReceived is not decimal c ? "—"
            : c >= Math.Round(GrandTotal) ? $"PKR {c - Math.Round(GrandTotal):N0}"
            : $"PKR {Math.Round(GrandTotal) - c:N0} short";
        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }
        public bool IsContinuousScanMode { get => _isContinuousScanMode; set => SetProperty(ref _isContinuousScanMode, value); }
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (!SetProperty(ref _isBusy, value)) return;
                ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
            }
        }

        public ICommand SearchCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CompleteSaleReportedCommand { get; }
        public ICommand CompleteSaleInternalCommand { get; }
        public ICommand PrintBillCommand { get; }
        public ICommand ClearCartCommand { get; }

        public async Task SearchMedicinesAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(SearchMedicineText))
            {
                _dispatcher.TryEnqueue(() => _medicineResults.Clear());
                return;
            }

            IsSearching = true;
            try
            {
                var results = await _medicineRepository.SearchAsync(SearchMedicineText);
                if (cancellationToken.IsCancellationRequested) return;
                _dispatcher.TryEnqueue(() => _medicineResults.ReplaceAll(results));
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) return;
                AppLogger.LogError("SearchMedicinesAsync failed", ex);
            }
            finally
            {
                if (!cancellationToken.IsCancellationRequested)
                    _dispatcher.TryEnqueue(() => IsSearching = false);
            }
        }

        /// <summary>
        /// Search right now for exactly this text (cancels the pending debounce). Used on Enter,
        /// so the decision is never made on results from earlier, shorter text.
        /// </summary>
        public async Task<List<Medicine>> SearchNowAsync(string text)
        {
            _searchCts?.Cancel();
            var results = await _medicineRepository.SearchAsync(text);
            _medicineResults.ReplaceAll(results);
            return results;
        }

        private async Task DebouncedSearchAsync()
        {
            _searchCts?.Cancel();

            if (string.IsNullOrWhiteSpace(SearchMedicineText))
            {
                _dispatcher.TryEnqueue(() => _medicineResults.Clear());
                return;
            }

            var cts = new CancellationTokenSource();
            _searchCts = cts;

            try
            {
                await Task.Delay(300, cts.Token);
            }
            catch (TaskCanceledException) { return; }

            if (cts.Token.IsCancellationRequested) return;

            await SearchMedicinesAsync(cts.Token);
        }

        public async Task<bool> ProcessBarcodeAsync(string barcode, bool silentFail = false)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return false;
            barcode = barcode.Trim();
            barcode = new string(barcode.Where(c => !char.IsControl(c)).ToArray());
            if (string.IsNullOrWhiteSpace(barcode)) return false;

            var medicine = await _medicineRepository.GetByBarcodeAsync(barcode);
            if (medicine == null)
            {
                if (!silentFail)
                {
                    IsStatusSuccess = false;
                    StatusMessage = $"No medicine found with barcode '{barcode}'.";
                }
                return false;
            }

            await ExecuteAddToCartAsync(medicine);
            SearchMedicineText = string.Empty;
            BarcodeText = string.Empty;
            return true;
        }

        /// <summary>Cart row last added or bumped; the page focuses its quantity box.</summary>
        public SaleItemViewModel? LastTouchedItem { get; private set; }

        /// <summary>Adds the medicine (or bumps its qty) and sets LastTouchedItem.</summary>
        public async Task ExecuteAddToCartAsync(Medicine? medicine = null)
        {
            var med = medicine ?? SelectedMedicine;
            LastTouchedItem = null;
            if (med == null) return;

            var batches = await _batchRepository.GetByMedicineIdAsync(med.Id);
            var activeBatches = batches
                .Where(b => b.RemainingUnits > 0 && b.ExpiryDate > DateTime.Today)
                .OrderBy(b => b.ExpiryDate)
                .ToList();

            if (!activeBatches.Any())
            {
                IsStatusSuccess = false;
                bool hasStockButExpired = batches.Any(b => b.RemainingUnits > 0 && b.ExpiryDate <= DateTime.Today);
                StatusMessage = hasStockButExpired ? $"'{med.Name}' is expired and cannot be sold." : $"'{med.Name}' is out of stock.";
                return;
            }

            var bestBatch = activeBatches.First();
            var existing = CartItems.FirstOrDefault(i => i.MedicineId == med.Id && i.BatchId == bestBatch.Id);
            SaleItemViewModel touched;
            if (existing != null)
            {
                existing.Quantity++;
                touched = existing;
            }
            else
            {
                var newItem = new SaleItemViewModel
                {
                    MedicineId = med.Id,
                    BatchId = bestBatch.Id,
                    MedicineName = med.Name,
                    BaseUnitPrice = bestBatch.SellingPrice,
                    StockUnits = activeBatches.Sum(b => b.RemainingUnits),
                    UnitsPerBox = (med.PacketsPerBox > 0 ? med.PacketsPerBox : 1) * (med.UnitsPerPack > 0 ? med.UnitsPerPack : 1),
                    QuantityBoxText = string.Empty, // Start blank
                    QuantityTabletText = string.Empty // Start blank
                };
                newItem.PropertyChanged += OnItemPropertyChanged;
                CartItems.Add(newItem);
                touched = newItem;
            }

            UpdateTotals();
            ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
            LastTouchedItem = touched;
        }

        private void ExecuteRemoveFromCart(SaleItemViewModel? item)
        {
            if (item == null) return;
            item.PropertyChanged -= OnItemPropertyChanged;
            CartItems.Remove(item);
            UpdateTotals();
            ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
        }

        private void ExecuteClearCart()
        {
            CartItems.Clear();
            UpdateTotals();
            CustomerName = string.Empty;
            CustomerPhone = string.Empty;
            CashReceivedText = string.Empty;
            _discountText = _discountPercentText = string.Empty;
            _discountRupees = _discountPercent = 0;
            _usePercentageDiscount = false;
            OnPropertyChanged(nameof(DiscountText));
            OnPropertyChanged(nameof(DiscountPercentText));
            UpdateTotals();
            ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
            ((RelayCommand)ClearCartCommand).RaiseCanExecuteChanged();
        }

        private void OnItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SaleItemViewModel.Quantity) || e.PropertyName == nameof(SaleItemViewModel.Subtotal))
                UpdateTotals();
        }

        private void UpdateTotals()
        {
            TotalAmount = CartItems.Sum(i => i.Subtotal);
            var totals = BillingTotals.Calculate(TotalAmount, _taxRate, _discountRupees, _discountPercent, _usePercentageDiscount, 0);
            TaxAmount = totals.Tax;
            DiscountAmount = totals.Discount;
            GrandTotal = totals.Total;
            OnPropertyChanged(nameof(ChangeText));
            OnPropertyChanged(nameof(IsChangeShort));
            OnPropertyChanged(nameof(IsChangeOk));
            OnPropertyChanged(nameof(ItemCountText));
        }

        private async Task ExecutePrintBillAsync()
        {
            if (!ValidateAdjustments()) return;
            var req = CreatePrintReceiptRequest("BILL-" + DateTime.Now.Ticks.ToString().Substring(10), null);
            var result = await _salesWorkflow.PrintReceiptAsync(req);
            if (!result.Success)
            {
                IsStatusSuccess = false;
                StatusMessage = "Sale finished, but receipt printing failed: " + result.Message;
            }
        }

        private PrintReceiptRequest CreatePrintReceiptRequest(string billNo, string? fbrInvoiceNo)
        {
            return new PrintReceiptRequest
            {
                BillNo = billNo,
                FbrInvoiceNo = fbrInvoiceNo,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                TotalAmount = TotalAmount,
                TaxAmount = TaxAmount,
                DiscountAmount = DiscountAmount,
                GrandTotal = GrandTotal,
                TaxRate = _taxRate,
                CashReceived = CashReceived,
                Items = CartItems.Select(i => new SaleLineItemDto
                {
                    MedicineId = i.MedicineId,
                    BatchId = i.BatchId,
                    MedicineName = i.MedicineName,
                    QuantityForReceipt = i.Quantity,
                    QuantityUnitsForStock = i.TotalTablets,
                    UnitPrice = i.UnitPrice,
                    Subtotal = i.Subtotal
                }).ToList()
            };
        }

        private bool ValidateAdjustments()
        {
            foreach (var input in new[] { DiscountText, DiscountPercentText })
            {
                if (!string.IsNullOrWhiteSpace(input) &&
                    (!decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0))
                {
                    StatusMessage = "Enter a valid, non-negative discount.";
                    return false;
                }
            }
            if (_usePercentageDiscount && _discountPercent > 100)
            {
                StatusMessage = "Discount percentage cannot exceed 100%.";
                return false;
            }
            return true;
        }

        private async Task ExecuteCompleteSaleAsync(bool shouldPrint)
        {
            if (!CartItems.Any() || IsBusy) return;
            if (!ValidateAdjustments()) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            IsStatusSuccess = false;
            try
            {
                StatusMessage = "Saving transaction...";

                var request = new CompleteSaleRequest
                {
                    CustomerName = CustomerName,
                    CustomerPhone = CustomerPhone,
                    TotalAmount = TotalAmount,
                    TaxAmount = TaxAmount,
                    DiscountAmount = DiscountAmount,
                    GrandTotal = GrandTotal,
                    ReportToFbr = false, // Not using FBR here
                    Items = CartItems.Select(i => new SaleLineItemDto
                    {
                        MedicineId = i.MedicineId,
                        BatchId = i.BatchId,
                        MedicineName = i.MedicineName,
                        QuantityForReceipt = i.Quantity,
                        QuantityUnitsForStock = i.TotalTablets,
                        UnitPrice = i.UnitPrice,
                        Subtotal = i.Subtotal
                    }).ToList()
                };

                var saleResult = await _salesWorkflow.CompleteSaleAsync(request);
                if (!saleResult.Success)
                {
                    IsStatusSuccess = false;
                    StatusMessage = saleResult.Message;
                    return;
                }

                var printReq = shouldPrint && saleResult.BillNo != null
                    ? CreatePrintReceiptRequest(saleResult.BillNo, saleResult.FbrInvoiceNo)
                    : null;
                // The bill is already saved. Clear it before printing so a printer error
                // cannot let the next shortcut save the same cart a second time.
                ExecuteClearCart();
                if (printReq != null)
                {
                    var printResult = await _salesWorkflow.PrintReceiptAsync(printReq);
                    if (!printResult.Success)
                    {
                        IsStatusSuccess = false;
                        StatusMessage = "Sale finished, but receipt printing failed: " + printResult.Message;
                        return;
                    }
                }

                IsStatusSuccess = true;
                StatusMessage = shouldPrint ? "Sale completed and printed successfully!" : "Sale saved successfully!";
                ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
            }
            catch (Exception ex)
            {
                IsStatusSuccess = false;
                StatusMessage = "Sale failed: " + ex.Message;
                AppLogger.LogError("BillingViewModel.ExecuteCompleteSaleAsync failed", ex);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class SaleItemViewModel : ViewModelBase
    {
        public int MedicineId { get; set; }
        public int BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int UnitsPerBox { get; set; } = 1;
        public decimal BaseUnitPrice { get; set; }
        /// <summary>Sellable (unexpired) units in stock when added — shown under the name.</summary>
        public int StockUnits { get; set; }
        public string StockHint => UnitsPerBox > 1 ? $"{StockUnits} tabs in stock · {UnitsPerBox}/box" : $"{StockUnits} in stock";

        private int _quantityBox = 0;
        public int QuantityBox
        {
            get => _quantityBox;
            set { if (SetProperty(ref _quantityBox, value)) { _quantityBoxText = value.ToString(); OnPropertyChanged(nameof(QuantityBoxText)); Recalculate(); } }
        }

        private string _quantityBoxText = string.Empty;
        public string QuantityBoxText
        {
            get => _quantityBoxText;
            set
            {
                if (SetProperty(ref _quantityBoxText, value))
                {
                    if (string.IsNullOrWhiteSpace(value)) { _quantityBox = 0; Recalculate(); }
                    else if (int.TryParse(value, out int result)) { _quantityBox = result; Recalculate(); }
                }
            }
        }

        private int _quantityTablet = 0;
        public int QuantityTablet
        {
            get => _quantityTablet;
            set { if (SetProperty(ref _quantityTablet, value)) { _quantityTabletText = value.ToString(); OnPropertyChanged(nameof(QuantityTabletText)); Recalculate(); } }
        }

        private string _quantityTabletText = string.Empty;
        public string QuantityTabletText
        {
            get => _quantityTabletText;
            set
            {
                if (SetProperty(ref _quantityTabletText, value))
                {
                    if (string.IsNullOrWhiteSpace(value)) { _quantityTablet = 0; Recalculate(); }
                    else if (int.TryParse(value, out int result)) { _quantityTablet = result; Recalculate(); }
                }
            }
        }

        public decimal UnitPrice => BaseUnitPrice; // Always show unit price for clarity

        public decimal Subtotal => BaseUnitPrice * TotalTablets;
        public int TotalTablets => (QuantityBox * UnitsPerBox) + QuantityTablet;
        
        // For compatibility with existing logic (e.g. Quantity++)
        public int Quantity
        {
            get => TotalTablets;
            set
            {
                if (UnitsPerBox > 1)
                {
                    QuantityBox = value / UnitsPerBox;
                    QuantityTablet = value % UnitsPerBox;
                }
                else
                {
                    QuantityTablet = value;
                }
                Recalculate();
            }
        }

        private void Recalculate()
        {
            OnPropertyChanged(nameof(TotalTablets));
            OnPropertyChanged(nameof(Quantity));
            OnPropertyChanged(nameof(Subtotal));
        }
    }
}
