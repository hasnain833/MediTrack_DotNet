using System;
using System.Collections.ObjectModel;
using System.Linq;
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
        private decimal _discountPercentage;
        private string _discountText = "0";
        private decimal _grandTotal;
        private Medicine? _selectedMedicine;
        private string _barcodeText = string.Empty;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private bool _isStatusSuccess;
        private bool _isContinuousScanMode = true;
        private bool _isSearching;

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
            MedicineResults = new ObservableCollection<Medicine>();

            SearchCommand = new AsyncRelayCommand(async _ => await SearchMedicinesAsync());
            AddToCartCommand = new AsyncRelayCommand(async _ => await ExecuteAddToCartAsync(), _ => SelectedMedicine != null);
            RemoveFromCartCommand = new RelayCommand(item => ExecuteRemoveFromCart(item as SaleItemViewModel), item => item is SaleItemViewModel);
            CompleteSaleReportedCommand = new AsyncRelayCommand(async _ => await ExecuteCompleteSaleAsync(true), _ => CartItems.Any());
            CompleteSaleInternalCommand = new AsyncRelayCommand(async _ => await ExecuteCompleteSaleAsync(false), _ => CartItems.Any());
            PrintBillCommand = new AsyncRelayCommand(async _ => await ExecutePrintBillAsync());
            ClearCartCommand = new RelayCommand(_ => ExecuteClearCart(), _ => CartItems.Any());
        }

        public async Task InitializeAsync()
        {
            _taxRate = await _settingsService.GetTaxRateAsync();
            OnPropertyChanged(nameof(TaxRateText));
        }

        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public bool IsStatusSuccess { get => _isStatusSuccess; set => SetProperty(ref _isStatusSuccess, value); }

        public ObservableCollection<SaleItemViewModel> CartItems { get; }
        public ObservableCollection<Medicine> MedicineResults { get; }

        public string SearchMedicineText
        {
            get => _searchMedicineText;
            set { if (SetProperty(ref _searchMedicineText, value)) _ = SearchMedicinesAsync(); }
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
        public decimal DiscountAmount { get => _discountAmount; set { if (SetProperty(ref _discountAmount, value)) UpdateTotals(); } }
        public string DiscountText
        {
            get => _discountText;
            set
            {
                if (SetProperty(ref _discountText, value))
                {
                    if (decimal.TryParse(value, out var d))
                        _discountPercentage = d;
                    else if (string.IsNullOrWhiteSpace(value))
                        _discountPercentage = 0;

                    UpdateTotals();
                }
            }
        }
        public decimal GrandTotal { get => _grandTotal; set => SetProperty(ref _grandTotal, value); }
        public string BarcodeText
        {
            get => _barcodeText;
            set => SetProperty(ref _barcodeText, value);
        }
        public bool IsContinuousScanMode { get => _isContinuousScanMode; set => SetProperty(ref _isContinuousScanMode, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand SearchCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CompleteSaleReportedCommand { get; }
        public ICommand CompleteSaleInternalCommand { get; }
        public ICommand PrintBillCommand { get; }
        public ICommand ClearCartCommand { get; }

        private async Task SearchMedicinesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchMedicineText))
            {
                _dispatcher.TryEnqueue(() => MedicineResults.Clear());
                return;
            }

            IsSearching = true;
            try
            {
                var results = await _medicineRepository.SearchAsync(SearchMedicineText);

                _dispatcher.TryEnqueue(() =>
                {
                    MedicineResults.Clear();
                    foreach (var r in results)
                        MedicineResults.Add(r);
                });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("SearchMedicinesAsync failed", ex);
            }
            finally
            {
                _dispatcher.TryEnqueue(() => IsSearching = false);
            }
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

        public async Task ExecuteAddToCartAsync(Medicine? medicine = null)
        {
            var med = medicine ?? SelectedMedicine;
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
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var newItem = new SaleItemViewModel
                {
                    MedicineId = med.Id,
                    BatchId = bestBatch.Id,
                    MedicineName = med.Name,
                    BaseUnitPrice = bestBatch.SellingPrice,
                    UnitsPerPack = bestBatch.UnitsPerPack > 0 ? bestBatch.UnitsPerPack : 1,
                    SelectedUnit = "Tablet",
                    Quantity = 1
                };
                newItem.PropertyChanged += OnItemPropertyChanged;
                CartItems.Add(newItem);
            }

            UpdateTotals();
            ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
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
            TaxAmount = TotalAmount * _taxRate;
            DiscountAmount = TotalAmount * (_discountPercentage / 100m);
            GrandTotal = TotalAmount + TaxAmount - DiscountAmount;
        }

        private async Task ExecutePrintBillAsync()
        {
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

        private async Task ExecuteCompleteSaleAsync(bool reportToFbr)
        {
            if (!CartItems.Any()) return;

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
                    ReportToFbr = reportToFbr,
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

                if (saleResult.BillNo != null)
                {
                    var printReq = CreatePrintReceiptRequest(saleResult.BillNo, saleResult.FbrInvoiceNo);
                    var printResult = await _salesWorkflow.PrintReceiptAsync(printReq);
                    if (!printResult.Success)
                    {
                        IsStatusSuccess = false;
                        StatusMessage = "Sale finished, but receipt printing failed: " + printResult.Message;
                    }
                }

                CartItems.Clear();
                UpdateTotals();
                CustomerName = string.Empty;
                CustomerPhone = string.Empty;

                IsStatusSuccess = true;
                StatusMessage = saleResult.FbrFailedButSavedLocally
                    ? $"Sale saved locally, but FBR reporting failed: {saleResult.Message}"
                    : saleResult.Message;

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
        public int UnitsPerPack { get; set; } = 1;
        public decimal BaseUnitPrice { get; set; }

        private string _selectedUnit = "Tablet";
        public string SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value))
                {
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(Subtotal));
                }
            }
        }

        public ObservableCollection<string> AvailableUnits { get; } = new() { "Tablet", "Packet", "Box" };

        public decimal UnitPrice
        {
            get
            {
                if (SelectedUnit == "Tablet") return BaseUnitPrice;
                return BaseUnitPrice * UnitsPerPack;
            }
            set { }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(Subtotal)); }
        }

        public decimal Subtotal => UnitPrice * Quantity;
        public int TotalTablets => SelectedUnit == "Tablet" ? Quantity : Quantity * UnitsPerPack;
    }
}
