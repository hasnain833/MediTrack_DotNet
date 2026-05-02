using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
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
        private readonly IDialogService _dialogService;
        private readonly IFinancialActionsService _financialActionsService;
        private SaleSummary? _selectedSale;
        private string _searchInvoiceTerm = string.Empty;
        private DateTimeOffset? _searchDate;
        private string _searchCustomerTerm = string.Empty;
        private Sale? _selectedSaleDetails;
        private bool _isDetailsLoading;
        private string _statusMessage = "Loading bills...";
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcher = App.MainRoot?.DispatcherQueue ?? Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public FinancialViewModel(
            SaleRepository saleRepo,
            IReportingService reportingService,
            AuthService authService,
            IDialogService dialogService,
            IFinancialActionsService financialActionsService)
        {
            _saleRepo = saleRepo;
            _reportingService = reportingService;
            _authService = authService;
            _dialogService = dialogService;
            _financialActionsService = financialActionsService;
            _dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            SalesHistory = new ObservableCollection<SaleSummary>();
            RevenueStats = new ObservableCollection<RevenueStat>();
            SelectedInvoiceItems = new ObservableCollection<InvoiceItemViewModel>();

            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportSalesToCsvAsync(SalesHistory));
            VoidSaleCommand = new AsyncRelayCommand(ExecuteVoidSaleAsync, CanExecuteSaleAction);
            ReprintReceiptCommand = new AsyncRelayCommand(ExecuteReprintReceiptAsync, CanExecuteSaleAction);
            SearchCommand = new AsyncRelayCommand(async _ => await LoadDataAsync());
            ExecuteReturnCommand = new AsyncRelayCommand(item => ExecuteReturnAsync(item as InvoiceItemViewModel));
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
        public ICommand ExportCommand { get; }
        public ICommand VoidSaleCommand { get; }
        public ICommand ReprintReceiptCommand { get; }
        public ICommand SearchCommand { get; }

        public async Task InitializeAsync()
        {
            // DB initialization runs in background at app startup.
            // Retry a few times so the first open of Financial page is resilient.
            const int attempts = 3;
            for (int i = 0; i < attempts; i++)
            {
                try
                {
                    await LoadDataAsync();
                    return;
                }
                catch
                {
                    if (i == attempts - 1) throw;
                    await Task.Delay(700);
                }
            }
        }

        private bool CanExecuteSaleAction(object? _) => SelectedSale != null && SelectedSale.Status != "Voided";

        private async Task LoadDataAsync()
        {
            try
            {
                var history = await _saleRepo.SearchInvoicesAsync(
                    SearchInvoiceTerm,
                    SearchDate?.DateTime,
                    SearchCustomerTerm);

                _dispatcher.TryEnqueue(() =>
                {
                    SalesHistory.Clear();
                    foreach (var item in history) SalesHistory.Add(item);
                    StatusMessage = history.Count == 0 ? "No bills found matching your search." : $"{history.Count} bills found.";
                });

                var todayStart = DateTime.Today;
                var todayEnd = DateTime.Today.AddDays(1).AddSeconds(-1);
                var dailyRev = await _saleRepo.GetRevenueTotalAsync(todayStart, todayEnd);

                var weekStart = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
                var weeklyRev = await _saleRepo.GetRevenueTotalAsync(weekStart, todayEnd);

                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
                var monthlyRev = await _saleRepo.GetRevenueTotalAsync(monthStart, todayEnd);

                _dispatcher.TryEnqueue(() =>
                {
                    RevenueStats.Clear();
                    RevenueStats.Add(new RevenueStat { Label = "Daily", Value = $"PKR {dailyRev:N2}", Change = "Real-time" });
                    RevenueStats.Add(new RevenueStat { Label = "Weekly", Value = $"PKR {weeklyRev:N2}", Change = "This Week" });
                    RevenueStats.Add(new RevenueStat { Label = "Monthly", Value = $"PKR {monthlyRev:N2}", Change = "This Month" });
                });
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Error loading bills. Please check connection.";
                AppLogger.LogError("FinancialViewModel.LoadDataAsync failed", ex);
            }
        }

        private async Task ExecuteVoidSaleAsync(object? _)
        {
            if (SelectedSale == null) return;

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Void Sale",
                $"Are you sure you want to void Bill # {SelectedSale.BillNo}? This will restore the stock and mark the sale as Voided.",
                "Void",
                "Cancel");

            if (!confirm) return;

            int userId = _authService.CurrentUser?.Id ?? 0;
            var result = await _financialActionsService.VoidSaleAsync(SelectedSale.BillNo, userId);
            await _dialogService.ShowMessageAsync(result.Success ? "Success" : "Void Failed", result.Message);
            if (result.Success) await LoadDataAsync();
        }

        private async Task ExecuteReprintReceiptAsync(object? _)
        {
            if (SelectedSale == null) return;

            var result = await _financialActionsService.ReprintReceiptAsync(SelectedSale.BillNo, SelectedSale.Customer);
            await _dialogService.ShowMessageAsync(result.Success ? "Printed" : "Reprint Failed", result.Message);
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

                _dispatcher.TryEnqueue(() =>
                {
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
                                ReturnInputQty = 1
                            });
                        }
                    }
                });
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
                "Return",
                "Cancel");

            if (!confirm) return;

            int userId = _authService.CurrentUser?.Id ?? 0;
            var result = await _financialActionsService.ReturnItemAsync(item.Id, item.ReturnInputQty, userId);
            await _dialogService.ShowMessageAsync(result.Success ? "Success" : "Return Failed", result.Message);

            if (result.Success)
            {
                await LoadDataAsync();
                await LoadSelectedSaleDetailsAsync();
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
