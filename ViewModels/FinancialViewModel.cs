using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Services;
using DChemist.Utils;

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

        private SaleSummary? _selectedSale;

        public FinancialViewModel(SaleRepository saleRepo, IReportingService reportingService, 
            AuthService authService, IPrintService printService, IDialogService dialogService, IConfiguration configuration)
        {
            _saleRepo = saleRepo;
            _reportingService = reportingService;
            _authService = authService;
            _printService = printService;
            _dialogService = dialogService;
            _configuration = configuration;
            
            SalesHistory = new ObservableCollection<SaleSummary>();
            RevenueStats = new ObservableCollection<RevenueStat>();
            
            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportSalesToCsvAsync(SalesHistory));
            VoidSaleCommand = new AsyncRelayCommand(ExecuteVoidSaleAsync, CanExecuteSaleAction);
            ReprintReceiptCommand = new AsyncRelayCommand(ExecuteReprintReceiptAsync, CanExecuteSaleAction);
            RefundCommand = new AsyncRelayCommand(ExecuteRefundAsync, CanExecuteSaleAction);
            
            _ = LoadDataAsync();
        }

        public SaleSummary? SelectedSale
        {
            get => _selectedSale;
            set
            {
                if (SetProperty(ref _selectedSale, value))
                {
                    ((AsyncRelayCommand)VoidSaleCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)ReprintReceiptCommand).RaiseCanExecuteChanged();
                    ((AsyncRelayCommand)RefundCommand).RaiseCanExecuteChanged();
                }
            }
        }

        public ObservableCollection<SaleSummary> SalesHistory { get; }
        public ObservableCollection<RevenueStat> RevenueStats { get; }
        public System.Windows.Input.ICommand ExportCommand { get; }
        public System.Windows.Input.ICommand VoidSaleCommand { get; }
        public System.Windows.Input.ICommand ReprintReceiptCommand { get; }
        public System.Windows.Input.ICommand RefundCommand { get; }

        private bool CanExecuteSaleAction(object? parameter) => SelectedSale != null && SelectedSale.Status != "Voided";

        private async Task LoadDataAsync()
        {
            try
            {
                // 1. Load Sales History
                var history = await _saleRepo.GetAllSummariesAsync(50);
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
        private async Task ExecuteRefundAsync(object? parameter)
        {
            if (SelectedSale == null) return;

            try
            {
                var fullSale = await _saleRepo.GetSaleWithItemsAsync(SelectedSale.BillNo);
                if (fullSale == null) return;

                var itemsToReturn = await _dialogService.ShowRefundDialogAsync(fullSale);
                if (itemsToReturn == null || !itemsToReturn.Any()) return;

                bool confirmed = await _dialogService.ShowConfirmationAsync(
                    "Confirm Returns",
                    $"Are you sure you want to process {itemsToReturn.Sum(i => i.ReturnInputQty)} item(s) to be returned for Bill # {SelectedSale.BillNo}?",
                    "Confirm", "Cancel");

                if (!confirmed) return;

                int userId = _authService.CurrentUser?.Id ?? 0;
                foreach (var item in itemsToReturn)
                {
                    if (item.MedicineId.HasValue && item.BatchId.HasValue)
                    {
                        await _saleRepo.ProcessReturnAsync(fullSale.Id, item.MedicineId.Value, item.BatchId.Value, (int)item.ReturnInputQty, userId);
                    }
                }

                await _dialogService.ShowMessageAsync("Success", "Selected items have been returned and stock has been restored.");
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                await _dialogService.ShowMessageAsync("Refund Error", ex.Message);
            }
        }
    }

    public class RevenueStat
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Change { get; set; } = string.Empty;
    }
}
