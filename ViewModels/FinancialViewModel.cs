using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;
using System.Windows.Input;

namespace DChemist.ViewModels
{
    public class FinancialViewModel : ViewModelBase
    {
        private readonly SaleRepository _saleRepo;
        private readonly IReportingService _reportingService;
        private readonly AuthService _authService;
        private readonly IPrintService _printService;
        private readonly IDialogService _dialogService;
        private readonly IConfiguration _configuration;
        private readonly SettingsService _settingsService;
        private readonly IFiscalService _fiscalService;
        private SaleSummary? _selectedSale;
        private string _searchInvoiceTerm = string.Empty;
        private DateTimeOffset? _searchDate;
        private string _searchCustomerTerm = string.Empty;
        private Sale? _selectedSaleDetails;
        private bool _isDetailsLoading;

        public FinancialViewModel(SaleRepository saleRepo, IReportingService reportingService, 
            AuthService authService, IPrintService printService, IDialogService dialogService, 
            IConfiguration configuration, SettingsService settingsService, IFiscalService fiscalService)
        {
            _saleRepo = saleRepo;
            _reportingService = reportingService;
            _authService = authService;
            _printService = printService;
            _dialogService = dialogService;
            _configuration = configuration;
            _settingsService = settingsService;
            _fiscalService = fiscalService;
            
            SalesHistory = new ObservableCollection<SaleSummary>();
            RevenueStats = new ObservableCollection<RevenueStat>();
            SelectedInvoiceItems = new ObservableCollection<InvoiceItemViewModel>();
            
            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportSalesToCsvAsync(SalesHistory));
            VoidSaleCommand = new AsyncRelayCommand(ExecuteVoidSaleAsync, CanExecuteSaleAction);
            ReprintReceiptCommand = new AsyncRelayCommand(ExecuteReprintReceiptAsync, CanExecuteSaleAction);
            SearchCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            ExecuteReturnCommand = new AsyncRelayCommand(item => ExecuteReturnAsync(item as InvoiceItemViewModel));
            
            _ = LoadDataAsync();
        }

        public ICommand ExecuteReturnCommand { get; }

        public SaleSummary? SelectedSale
        {
            get => _selectedSale;
            set
            {
                if (SetProperty(ref _selectedSale, value))
                {
                    ((AsyncRelayCommand)VoidSaleCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)ReprintReceiptCommand).RaiseCanExecuteChanged();
                    _ = LoadSelectedSaleDetailsAsync();
                }
            }
        }

        public string SearchInvoiceTerm
        {
            get => _searchInvoiceTerm;
            set { if (SetProperty(ref _searchInvoiceTerm, value)) _ = LoadDataAsync(); }
        }

        public DateTimeOffset? SearchDate
        {
            get => _searchDate;
            set { if (SetProperty(ref _searchDate, value)) _ = LoadDataAsync(); }
        }

        public string SearchCustomerTerm
        {
            get => _searchCustomerTerm;
            set { if (SetProperty(ref _searchCustomerTerm, value)) _ = LoadDataAsync(); }
        }

        public Sale? SelectedSaleDetails
        {
            get => _selectedSaleDetails;
            set => SetProperty(ref _selectedSaleDetails, value);
        }

        public bool IsDetailsLoading
        {
            get => _isDetailsLoading;
            set => SetProperty(ref _isDetailsLoading, value);
        }

        public ObservableCollection<InvoiceItemViewModel> SelectedInvoiceItems { get; }

        public ObservableCollection<SaleSummary> SalesHistory { get; }
        public ObservableCollection<RevenueStat> RevenueStats { get; }
        public System.Windows.Input.ICommand ExportCommand { get; }
        public System.Windows.Input.ICommand VoidSaleCommand { get; }
        public System.Windows.Input.ICommand ReprintReceiptCommand { get; }
        public System.Windows.Input.ICommand SearchCommand { get; }

        private bool CanExecuteSaleAction(object? parameter) => SelectedSale != null && SelectedSale.Status != "Voided";

        private async Task LoadDataAsync()
        {
            try
            {
                // 1. Load Sales History with filters
                var history = await _saleRepo.SearchInvoicesAsync(
                    SearchInvoiceTerm, 
                    SearchDate?.DateTime, 
                    SearchCustomerTerm);
                
                SalesHistory.Clear();
                foreach (var item in history) SalesHistory.Add(item);

                // 2. Load Revenue Stats
                RevenueStats.Clear();
                
                var todayStart = DateTime.Today;
                var todayEnd = DateTime.Today.AddDays(1).AddSeconds(-1);
                var dailyRev = await _saleRepo.GetRevenueTotalAsync(todayStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Daily", Value = $"PKR {dailyRev:N2}", Change = "Real-time" });

                var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var weeklyRev = await _saleRepo.GetRevenueTotalAsync(weekStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Weekly", Value = $"PKR {weeklyRev:N2}", Change = "This Week" });

                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthlyRev = await _saleRepo.GetRevenueTotalAsync(monthStart, todayEnd);
                RevenueStats.Add(new RevenueStat { Label = "Monthly", Value = $"PKR {monthlyRev:N2}", Change = "This Month" });
            }
            catch (Exception ex)
            {
                AppLogger.LogError("FinancialViewModel.LoadDataAsync failed", ex);
            }
        }

        private async Task ExecuteVoidSaleAsync(object? parameter)
        {
            if (SelectedSale == null) return;
            
            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Void Sale", 
                $"Are you sure you want to void Bill # {SelectedSale.BillNo}? This will restore the stock and mark the sale as Voided.",
                "Void", "Cancel");
            
            if (!confirm) return;

            try
            {
                await _saleRepo.VoidSaleAsync(SelectedSale.BillNo, _authService.CurrentUser?.Id ?? 0);
                await _dialogService.ShowMessageAsync("Success", "Sale has been voided successfully.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Void Failed", ex.Message);
            }
        }

        private async Task ExecuteReprintReceiptAsync(object? parameter)
        {
            if (SelectedSale == null) return;

            try
            {
                var fullSale = await _saleRepo.GetSaleWithItemsAsync(SelectedSale.BillNo);
                if (fullSale == null) throw new Exception("Could not retrieve full sale details.");

                var printerName = _configuration["Printing:ThermalPrinterName"];
                
                var receiptVM = new ReceiptViewModel
                {
                    BillNo = fullSale.BillNo,
                    CustomerName = SelectedSale.Customer,
                    TotalAmount = fullSale.TotalAmount,
                    TaxAmount = fullSale.TaxAmount,
                    DiscountAmount = fullSale.DiscountAmount,
                    GrandTotal = fullSale.GrandTotal,
                    FbrInvoiceNo = fullSale.Status == "Voided" ? "VOIDED - DO NOT USE" : "SIM-FBR-" + fullSale.BillNo
                };

                var taxRate = Convert.ToDecimal(_configuration["TaxRate"] ?? "0.18");
                receiptVM.TaxRateText = $"Tax ({taxRate * 100:0.##}%):";

                foreach (var item in fullSale.Items)
                {
                    receiptVM.Items.Add(new ReceiptItemViewModel
                    {
                        Name = item.MedicineName,
                        Quantity = item.Quantity,
                        Price = item.UnitPrice
                    });
                }

                if (!string.IsNullOrEmpty(printerName))
                {
                    await receiptVM.LoadStoreDetailsAsync(_settingsService);
                    await receiptVM.InitializeQrCode(_fiscalService);
                    await _printService.PrintReceiptSilentAsync(receiptVM, printerName);
                    await _dialogService.ShowMessageAsync("Printed", "Receipt sent to printer.");
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Info", "Printer name not configured. Please add Printing:ThermalPrinterName to appsettings.json for silent printing.");
                }
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Reprint Failed", ex.Message);
            }
        }
        private async Task LoadSelectedSaleDetailsAsync()
        {
            if (SelectedSale == null)
            {
                SelectedSaleDetails = null;
                SelectedInvoiceItems.Clear();
                return;
            }

            IsDetailsLoading = true;
            try
            {
                var fullSale = await _saleRepo.GetSaleWithItemsAsync(SelectedSale.BillNo);
                SelectedSaleDetails = fullSale;
                
                SelectedInvoiceItems.Clear();
                if (fullSale != null)
                {
                    foreach (var item in fullSale.Items)
                    {
                        SelectedInvoiceItems.Add(new InvoiceItemViewModel
                        {
                            Id = item.Id,
                            MedicineName = item.MedicineName,
                            Quantity = item.Quantity,
                            ReturnedQuantity = item.ReturnedQuantity,
                            UnitPrice = item.UnitPrice,
                            Subtotal = item.Subtotal,
                            ReturnInputQty = 1 // default return qty
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to load sale details", ex);
            }
            finally
            {
                IsDetailsLoading = false;
            }
        }

        private async Task ExecuteReturnAsync(InvoiceItemViewModel? item)
        {
            if (item == null) return;
            if (item.ReturnInputQty <= 0) return;
            if (item.ReturnInputQty > item.RemainingQuantity)
            {
                await _dialogService.ShowMessageAsync("Invalid Quantity", "Return quantity cannot exceed remaining sold quantity.");
                return;
            }

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Confirm Return",
                $"Are you sure you want to return {item.ReturnInputQty} units of {item.MedicineName}?",
                "Return", "Cancel");

            if (!confirm) return;

            try
            {
                int userId = _authService.CurrentUser?.Id ?? 0;
                await _saleRepo.ProcessReturnAsync(item.Id, item.ReturnInputQty, userId);
                
                await _dialogService.ShowMessageAsync("Success", "Item returned and stock restored.");
                
                // Refresh data
                await LoadDataAsync();
                await LoadSelectedSaleDetailsAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Return Failed", ex.Message);
            }
        }
    }

    public class InvoiceItemViewModel : ViewModelBase
    {
        public int Id { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int ReturnedQuantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        
        private int _returnInputQty;
        public int ReturnInputQty
        {
            get => _returnInputQty;
            set => SetProperty(ref _returnInputQty, value);
        }

        public int RemainingQuantity => Quantity - ReturnedQuantity;
        public decimal CurrentTotal => RemainingQuantity * UnitPrice;
        public bool CanReturn => RemainingQuantity > 0;
    }

    public class RevenueStat
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
    }
}
