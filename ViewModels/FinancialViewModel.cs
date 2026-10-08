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

namespace DChemist.ViewModels
{
    /// <summary>Bills page: search + date range, bills grouped by day, bill detail with reprint / return / void.</summary>
    public class FinancialViewModel : ViewModelBase
    {
        private readonly SaleRepository _saleRepo;
        private readonly IReportingService _reportingService;
        private readonly AuthService _authService;
        private readonly IDialogService _dialogService;
        private readonly IFinancialActionsService _financialActionsService;
        private CancellationTokenSource? _searchCts;

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

            ExportCommand = new AsyncRelayCommand(async _ => await _reportingService.ExportSalesToCsvAsync(BillGroups.SelectMany(g => g)));
            VoidSaleCommand = new AsyncRelayCommand(ExecuteVoidSaleAsync, _ => CanAct);
            ReprintReceiptCommand = new AsyncRelayCommand(ExecuteReprintReceiptAsync, _ => CanAct);
            StartReturnCommand = new RelayCommand(_ => StartReturn(), _ => CanAct && SelectedInvoiceItems.Any(i => i.CanReturn));
            CancelReturnCommand = new RelayCommand(_ => IsReturnMode = false);
            ReturnAllCommand = new RelayCommand(_ => { foreach (var i in SelectedInvoiceItems) i.ReturnInputQty = i.RemainingQuantity; });
            ConfirmReturnCommand = new AsyncRelayCommand(ExecuteConfirmReturnAsync);
        }

        public ICommand ExportCommand { get; }
        public ICommand VoidSaleCommand { get; }
        public ICommand ReprintReceiptCommand { get; }
        public ICommand StartReturnCommand { get; }
        public ICommand CancelReturnCommand { get; }
        public ICommand ReturnAllCommand { get; }
        public ICommand ConfirmReturnCommand { get; }

        // ---------------- list ----------------
        public ObservableCollection<BillGroup> BillGroups { get; } = new();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = DebouncedLoadAsync(); }
        }

        /// <summary>today / yesterday / week / all</summary>
        private string _range = "today";
        public string Range
        {
            get => _range;
            set { if (SetProperty(ref _range, value)) _ = LoadDataAsync(); }
        }

        private int _billCount;
        public int BillCount { get => _billCount; private set => SetProperty(ref _billCount, value); }
        private decimal _billsTotal;
        public decimal BillsTotal { get => _billsTotal; private set => SetProperty(ref _billsTotal, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public async Task InitializeAsync()
        {
            // DB initialization runs in background at app startup; retry so the first open is resilient.
            for (int i = 0; i < 3; i++)
            {
                try { await LoadDataAsync(); return; }
                catch when (i < 2) { await Task.Delay(700); }
            }
        }

        // Typing fires once per pause, not once per key.
        private async Task DebouncedLoadAsync()
        {
            _searchCts?.Cancel();
            var cts = _searchCts = new CancellationTokenSource();
            try { await Task.Delay(300, cts.Token); } catch (TaskCanceledException) { return; }
            await LoadDataAsync();
        }

        private (DateTime? from, DateTime? to) RangeBounds()
        {
            var today = DateTime.Today;
            return Range switch
            {
                "today" => (today, today.AddDays(1)),
                "yesterday" => (today.AddDays(-1), today),
                "week" => (today.AddDays(-6), today.AddDays(1)),
                _ => (null, null)
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                var (from, to) = RangeBounds();
                var bills = await _saleRepo.SearchInvoicesAsync(SearchText, from, to);
                string? keepBillNo = SelectedSale?.BillNo;

                BillGroups.Clear();
                foreach (var day in bills.GroupBy(b => b.SaleDate.ToLocalTime().Date))
                    BillGroups.Add(new BillGroup(DayLabel(day.Key), day));

                var counted = bills.Where(b => b.Status != "Voided").ToList();
                BillCount = bills.Count;
                BillsTotal = counted.Sum(b => b.Amount);
                StatusMessage = bills.Count == 0 ? "No bills match." : string.Empty;

                // Clearing the list drops the selection; keep the same bill selected after a refresh.
                SelectedSale = keepBillNo == null ? null : bills.Find(b => b.BillNo == keepBillNo);
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Could not load bills. Check the database connection.";
                AppLogger.LogError("FinancialViewModel.LoadDataAsync failed", ex);
            }
        }

        private static string DayLabel(DateTime d) =>
            d == DateTime.Today ? $"Today · {d:d MMM}"
            : d == DateTime.Today.AddDays(-1) ? $"Yesterday · {d:d MMM}"
            : d.ToString("ddd, d MMM yyyy");

        // ---------------- selected bill ----------------
        private SaleSummary? _selectedSale;
        public SaleSummary? SelectedSale
        {
            get => _selectedSale;
            set
            {
                if (SetProperty(ref _selectedSale, value))
                {
                    IsReturnMode = false;
                    OnPropertyChanged(nameof(HasSelection));
                    RaiseCommandStates();
                    _ = LoadSelectedSaleDetailsAsync();
                }
            }
        }
        public bool HasSelection => SelectedSale != null;
        private bool CanAct => SelectedSale != null && SelectedSale.Status != "Voided";

        private Sale? _details;
        public Sale? SelectedSaleDetails { get => _details; private set { if (SetProperty(ref _details, value)) RaiseDetailTotals(); } }
        public ObservableCollection<InvoiceItemViewModel> SelectedInvoiceItems { get; } = new();

        public string DetailFacts => SelectedSaleDetails == null || SelectedSale == null ? string.Empty
            : $"{SelectedSaleDetails.SaleDate.ToLocalTime():ddd d MMM, HH:mm}   ·   {SelectedSale.Customer}" +
              (string.IsNullOrEmpty(SelectedSaleDetails.CashierName) ? "" : $"   ·   Cashier: {SelectedSaleDetails.CashierName}");
        public decimal DetailSubtotal => SelectedInvoiceItems.Sum(i => i.Quantity * i.UnitPrice);
        public decimal DetailDiscount => SelectedSaleDetails?.DiscountAmount ?? 0;
        public decimal DetailReturned => SelectedInvoiceItems.Sum(i => i.ReturnedQuantity * i.UnitPrice);
        public bool HasReturned => DetailReturned > 0;
        public bool HasDiscount => DetailDiscount > 0;
        public decimal DetailNet => SelectedSale?.Status == "Voided" ? 0 : SelectedSaleDetails?.GrandTotal ?? 0;
        public string NetLabel => SelectedSale?.Status == "Voided" ? "Voided" : "Net total";

        private bool _isDetailsLoading;
        public bool IsDetailsLoading { get => _isDetailsLoading; set => SetProperty(ref _isDetailsLoading, value); }

        private async Task LoadSelectedSaleDetailsAsync()
        {
            SelectedInvoiceItems.Clear();
            if (SelectedSale == null) { SelectedSaleDetails = null; return; }

            IsDetailsLoading = true;
            try
            {
                var sale = await _saleRepo.GetSaleWithItemsAsync(SelectedSale.BillNo);
                SelectedInvoiceItems.Clear();
                foreach (var item in sale?.Items ?? new List<SaleItem>())
                {
                    var row = new InvoiceItemViewModel
                    {
                        Id = item.Id,
                        MedicineName = item.MedicineName,
                        Quantity = item.Quantity,
                        ReturnedQuantity = item.ReturnedQuantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.Subtotal
                    };
                    row.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(InvoiceItemViewModel.ReturnInputQty)) OnPropertyChanged(nameof(RefundTotal)); };
                    SelectedInvoiceItems.Add(row);
                }
                SelectedSaleDetails = sale;
                RaiseCommandStates();
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

        private void RaiseDetailTotals()
        {
            foreach (var p in new[] { nameof(DetailFacts), nameof(DetailSubtotal), nameof(DetailDiscount), nameof(DetailReturned),
                                      nameof(HasReturned), nameof(HasDiscount), nameof(DetailNet), nameof(NetLabel) })
                OnPropertyChanged(p);
        }

        private void RaiseCommandStates()
        {
            ((AsyncRelayCommand)VoidSaleCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ReprintReceiptCommand).RaiseCanExecuteChanged();
            ((RelayCommand)StartReturnCommand).RaiseCanExecuteChanged();
        }

        // ---------------- return mode ----------------
        private bool _isReturnMode;
        public bool IsReturnMode
        {
            get => _isReturnMode;
            set
            {
                if (SetProperty(ref _isReturnMode, value))
                {
                    OnPropertyChanged(nameof(IsNotReturnMode));
                    foreach (var i in SelectedInvoiceItems) i.ReturnInputQty = 0;
                    OnPropertyChanged(nameof(RefundTotal));
                }
            }
        }
        public bool IsNotReturnMode => !IsReturnMode;

        /// <summary>What goes back to the customer — same per-unit amount SaleRepository deducts from the bill.</summary>
        public decimal RefundTotal => SelectedInvoiceItems.Sum(i => Math.Clamp(i.ReturnInputQty, 0, i.RemainingQuantity) * i.UnitPrice);

        private void StartReturn() => IsReturnMode = true;

        private async Task ExecuteConfirmReturnAsync(object? _)
        {
            if (SelectedSale == null) return;
            var lines = SelectedInvoiceItems.Where(i => i.ReturnInputQty > 0).ToList();
            if (lines.Count == 0) { IsReturnMode = false; return; }

            var tooMany = lines.FirstOrDefault(i => i.ReturnInputQty > i.RemainingQuantity);
            if (tooMany != null)
            {
                await _dialogService.ShowMessageAsync("Too many", $"Only {tooMany.RemainingQuantity} of {tooMany.MedicineName} can still be returned.");
                return;
            }

            string billNo = SelectedSale.BillNo, customer = SelectedSale.Customer;
            bool returnsAll = SelectedInvoiceItems.All(i => i.ReturnInputQty >= i.RemainingQuantity);
            int userId = _authService.CurrentUser?.Id ?? 0;
            foreach (var line in lines)
            {
                var result = await _financialActionsService.ReturnItemAsync(line.Id, line.ReturnInputQty, userId);
                if (!result.Success)
                {
                    await _dialogService.ShowMessageAsync("Return Failed", $"{line.MedicineName}: {result.Message}");
                    returnsAll = false;
                    break;
                }
            }

            _isReturnMode = false;
            OnPropertyChanged(nameof(IsReturnMode));
            OnPropertyChanged(nameof(IsNotReturnMode));
            if (returnsAll) SelectedSale = null; // the bill is voided and leaves the list
            await LoadDataAsync();
            await LoadSelectedSaleDetailsAsync();
            if (returnsAll) { StatusMessage = $"✔ Everything returned. Bill {billNo} removed and stock restored."; return; }

            // The customer needs the corrected bill.
            var print = await _financialActionsService.ReprintReceiptAsync(billNo, customer);
            StatusMessage = print.Success ? $"✔ Returned. Updated bill {billNo} sent to the printer." : $"Returned, but printing failed: {print.Message}";
        }

        // ---------------- reprint / void ----------------
        private async Task ExecuteReprintReceiptAsync(object? _)
        {
            if (SelectedSale == null) return;
            var result = await _financialActionsService.ReprintReceiptAsync(SelectedSale.BillNo, SelectedSale.Customer);
            if (!result.Success)
                await _dialogService.ShowMessageAsync("Reprint Failed", result.Message);
            else
                StatusMessage = $"✔ Bill {SelectedSale.BillNo} sent to the printer.";
        }

        private async Task ExecuteVoidSaleAsync(object? _)
        {
            if (SelectedSale == null) return;

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Void Sale",
                $"Void bill {SelectedSale.BillNo}? Stock is restored and the bill is removed from the list.",
                "Void",
                "Cancel");
            if (!confirm) return;

            int userId = _authService.CurrentUser?.Id ?? 0;
            var result = await _financialActionsService.VoidSaleAsync(SelectedSale.BillNo, userId);
            if (!result.Success) { await _dialogService.ShowMessageAsync("Void Failed", result.Message); return; }
            StatusMessage = $"✔ Bill {SelectedSale.BillNo} voided.";
            await LoadDataAsync();
            await LoadSelectedSaleDetailsAsync();
        }
    }

    /// <summary>One day's bills in the list, with the day's total in the header.</summary>
    public class BillGroup : List<SaleSummary>
    {
        public BillGroup(string label, IEnumerable<SaleSummary> bills) : base(bills)
        {
            Label = label;
            Total = this.Where(b => b.Status != "Voided").Sum(b => b.Amount);
        }
        public string Label { get; }
        public decimal Total { get; }
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
        public int ReturnInputQty { get => _returnInputQty; set => SetProperty(ref _returnInputQty, value); }

        public int RemainingQuantity => Quantity - ReturnedQuantity;
        public decimal CurrentTotal => RemainingQuantity * UnitPrice;
        public bool CanReturn => RemainingQuantity > 0;
        public bool HasReturns => ReturnedQuantity > 0;
        public string ReturnedText => ReturnedQuantity > 0 ? $"(−{ReturnedQuantity} returned)" : string.Empty;
        /// <summary>Fully returned lines are dimmed.</summary>
        public double RowOpacity => RemainingQuantity > 0 ? 1.0 : 0.45;
    }
}
