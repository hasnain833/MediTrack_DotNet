using System;
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
    public class BillingViewModel : ViewModelBase
    {
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher;
        private readonly MedicineRepository _medicineRepository;
        private readonly SaleRepository _saleRepository;
        private readonly CustomerRepository _customerRepository;
        private readonly BatchRepository _batchRepository;
        private readonly AuthService _authService;
        private readonly IPrintService _printService;
        private readonly IFiscalService _fiscalService;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;
        private readonly SettingsService _settingsService;

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
        private bool _isContinuousScanMode;
        private bool _isSearching;

        public bool IsSearching { get => _isSearching; set => SetProperty(ref _isSearching, value); }

        public BillingViewModel(MedicineRepository medicineRepository, SaleRepository saleRepository, 
                                 CustomerRepository customerRepository, BatchRepository batchRepository, 
                                 AuthService authService, IPrintService printService, IFiscalService fiscalService,
                                 Microsoft.Extensions.Configuration.IConfiguration configuration,
                                 SettingsService settingsService)
        {
            System.Diagnostics.Debug.WriteLine("[BillingViewModel] Constructor: Start.");
            _medicineRepository = medicineRepository;
            _saleRepository = saleRepository;
            _customerRepository = customerRepository;
            _batchRepository = batchRepository;
            _authService = authService;
            _printService = printService;
            _fiscalService = fiscalService;
            _configuration = configuration;
            _settingsService = settingsService;
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
            System.Diagnostics.Debug.WriteLine("[BillingViewModel] Constructor: Finished.");
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
            set { if (SetProperty(ref _barcodeText, value)) _ = HandleBarcodeScanAsync(); }
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
            if (string.IsNullOrWhiteSpace(SearchMedicineText)) { MedicineResults.Clear(); return; }
            IsSearching = true;
            try
            {
                var results = await _medicineRepository.SearchAsync(SearchMedicineText);
                MedicineResults.Clear();
                foreach (var r in results) MedicineResults.Add(r);
            }
            finally { IsSearching = false; }
        }

        private async Task HandleBarcodeScanAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeText)) return;
            
            var medicine = await _medicineRepository.GetByBarcodeAsync(BarcodeText);
            if (medicine != null)
            {
                SelectedMedicine = medicine;
                await ExecuteAddToCartAsync();
                BarcodeText = string.Empty; 
            }
        }

        public async Task ExecuteAddToCartAsync(Medicine? medicine = null)
        {
            var med = medicine ?? SelectedMedicine;
            if (med == null) return;
 
            var batches = await _batchRepository.GetByMedicineIdAsync(med.Id);
            var activeBatches = batches.Where(b => b.RemainingUnits > 0 && b.ExpiryDate > DateTime.Today).OrderBy(b => b.ExpiryDate).ToList();

            if (!activeBatches.Any())
            {
                IsStatusSuccess = false;
                bool hasStockButExpired = batches.Any(b => b.RemainingUnits > 0 && b.ExpiryDate <= DateTime.Today);
                StatusMessage = hasStockButExpired ? $"⚠ '{med.Name}' is expired and cannot be sold." : $"⚠ '{med.Name}' is out of stock.";
                return;
            }
            
            var bestBatch = activeBatches.First();
 
            var existing = CartItems.FirstOrDefault(i => i.MedicineId == med.Id);
            if (existing != null)
            {
                existing.Quantity++;
            }
            else
            {
                var newItem = new SaleItemViewModel
                {
                    MedicineId    = med.Id,
                    BatchId       = bestBatch.Id,
                    MedicineName  = med.Name,
                    UnitPrice     = bestBatch.SellingPrice,
                    Quantity      = 1
                };
                newItem.PropertyChanged += OnItemPropertyChanged;
                CartItems.Add(newItem);
            }
            UpdateTotals();
            ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];

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
            {
                UpdateTotals();
            }
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
            await PrintCurrentReceiptAsync("BILL-" + DateTime.Now.Ticks.ToString().Substring(10), null);
        }

        private async Task PrintCurrentReceiptAsync(string billNo, string? fbrInvNo)
        {
            var receiptVM = new ReceiptViewModel
            {
                BillNo = billNo,
                FbrInvoiceNo = fbrInvNo,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                TotalAmount = TotalAmount,
                TaxAmount = TaxAmount,
                TaxRateText = TaxRateText + ":",
                DiscountAmount = DiscountAmount,
                GrandTotal = GrandTotal
            };

            foreach (var item in CartItems)
            {
                receiptVM.Items.Add(new ReceiptItemViewModel
                {
                    Name = item.MedicineName,
                    Quantity = item.Quantity,
                    Price = item.UnitPrice
                });
            }

            _dispatcher.TryEnqueue(async () =>
            {
                try
                {
                    await receiptVM.LoadStoreDetailsAsync(_settingsService);
                    await receiptVM.InitializeQrCode(_fiscalService);
    
                    var receiptControl = new Views.ReceiptTemplate(receiptVM);
                    
                    await _printService.PrintReceiptAsync(receiptControl, "Sale Receipt " + billNo);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"Printing failed for {billNo}", ex);
                    IsStatusSuccess = false;
                    StatusMessage = "⚠ Sale finished, but receipt printing failed.";
                }
            });
            await Task.CompletedTask;
        }
    
        public async Task<Views.ReceiptTemplate> CreateReceiptPreviewAsync()
        {
            string billNo = "BILL-PREVIEW";
            var receiptVM = new ReceiptViewModel
            {
                BillNo = billNo,
                FbrInvoiceNo = null,
                CustomerName = CustomerName,
                CustomerPhone = CustomerPhone,
                TotalAmount = TotalAmount,
                TaxAmount = TaxAmount,
                TaxRateText = TaxRateText + ":",
                DiscountAmount = DiscountAmount,
                GrandTotal = GrandTotal
            };

            foreach (var item in CartItems)
            {
                receiptVM.Items.Add(new ReceiptItemViewModel
                {
                    Name = item.MedicineName,
                    Quantity = item.Quantity,
                    Price = item.UnitPrice
                });
            }

            await receiptVM.LoadStoreDetailsAsync(_settingsService);
            await receiptVM.InitializeQrCode(_fiscalService);

            return new Views.ReceiptTemplate(receiptVM);
        }

        private async Task ExecuteCompleteSaleAsync(bool reportToFbr)
        {
            if (_authService.CurrentUser == null) return;
            if (!CartItems.Any()) return;

            IsBusy = true;
            StatusMessage = string.Empty;
            IsStatusSuccess = false;
            try
            {
                // Stock validation check across all items first
                foreach (var item in CartItems)
                {
                    var totalStock = await _batchRepository.GetTotalStockAsync(item.MedicineId);
                    if (totalStock < item.Quantity)
                    {
                        IsStatusSuccess = false;
                        StatusMessage = $"⚠ Insufficient total stock for '{item.MedicineName}'. Available: {totalStock}";
                        IsBusy = false;
                        return;
                    }
                }

                int? customerId = null;
                if (!string.IsNullOrWhiteSpace(CustomerName))
                {
                    var customer = await _customerRepository.FindOrCreateAsync(CustomerName, CustomerPhone);
                    customerId = customer?.Id;
                }

                string billNo = "INV-" + DateTime.Now.Ticks.ToString().Substring(10);
                var items = CartItems.Select(i => new SaleItem { 
                    MedicineId      = i.MedicineId, 
                    BatchId         = i.BatchId,
                    MedicineName    = i.MedicineName,
                    Quantity        = i.Quantity, 
                    UnitPrice       = i.UnitPrice, 
                    Subtotal        = i.Subtotal
                }).ToList();

                StatusMessage = "Saving transaction...";
                var saleId = await _saleRepository.CreateTransactionAsync(billNo, _authService.CurrentUser.Id, customerId, items, 
                    TotalAmount, TaxAmount, DiscountAmount, GrandTotal, false, null, null);

                string? fbrInvNo = null;
                string? fbrResponseRaw = null;

                if (reportToFbr)
                {
                    StatusMessage = "Reporting to FBR...";
                    var fbrResponse = await _fiscalService.ReportSaleAsync(billNo, GrandTotal, TaxAmount, items);
                    fbrInvNo = fbrResponse.Success ? fbrResponse.InvoiceNumber : null;
                    fbrResponseRaw = fbrResponse.ResponseRaw;
                    
                    if (fbrResponse.Success)
                    {
                        // Update the local record with FBR info
                        await _saleRepository.UpdateFbrStatusAsync(saleId, fbrInvNo, fbrResponseRaw);
                    }
                    else
                    {
                        // Don't abort the sale since it's already saved locally, but warn the user
                        IsStatusSuccess = true; // Still true because stock is deducted and saved
                        StatusMessage = $"✅ Sale saved locally, but ⚠ FBR Reporting Failed: {fbrResponse.ErrorMessage}";
                        await PrintCurrentReceiptAsync(billNo, null);
                        goto Cleanup;
                    }
                }

                await PrintCurrentReceiptAsync(billNo, fbrInvNo);

            Cleanup:
                CartItems.Clear();
                UpdateTotals();
                CustomerName = "";
                CustomerPhone = "";
                IsStatusSuccess = true;
                StatusMessage = reportToFbr ? "✅ Sale completed & reported to FBR." : "✅ Sale saved internally only.";
                
                ((AsyncRelayCommand)CompleteSaleReportedCommand).RaiseCanExecuteChanged();
                ((AsyncRelayCommand)CompleteSaleInternalCommand).RaiseCanExecuteChanged();
            }
            catch (InvalidOperationException ex)
            {
                IsStatusSuccess = false;
                StatusMessage = "⚠ " + ex.Message;
            }
            catch (Exception ex)
            {
                IsStatusSuccess = false;
                StatusMessage = "⚠ Sale failed: " + ex.Message;
                System.Diagnostics.Debug.WriteLine($"[Billing] Sale error: {ex}");
            }
            finally { IsBusy = false; }
        }
    }

    public class SaleItemViewModel : ViewModelBase
    {
        public int MedicineId { get; set; }
        public int BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;

        private decimal _unitPrice;
        public decimal UnitPrice
        {
            get => _unitPrice;
            set { if (SetProperty(ref _unitPrice, value)) OnPropertyChanged(nameof(Subtotal)); }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(Subtotal)); }
        }

        public decimal Subtotal => UnitPrice * Quantity;
    }
}
