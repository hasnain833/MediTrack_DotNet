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

namespace DChemist.ViewModels
{
    /// <summary>Invoices page: search + date range, invoices grouped by day, invoice detail with edit / delete.</summary>
    public class PurchaseHistoryViewModel : ViewModelBase
    {
        private readonly PurchaseInvoiceRepository _invoiceRepo;
        private readonly IDialogService _dialogService;
        private readonly DispatcherQueue _dispatcherQueue;
        private CancellationTokenSource? _searchCts;

        public PurchaseHistoryViewModel(PurchaseInvoiceRepository invoiceRepo, IDialogService dialogService, InventoryEventBus eventBus)
        {
            _invoiceRepo = invoiceRepo;
            _dialogService = dialogService;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            RefreshCommand = new AsyncRelayCommand(LoadAsync);
            DeleteInvoiceCommand = new AsyncRelayCommand(DeleteSelectedInvoiceAsync);
            EditInvoiceCommand = new RelayCommand(_ => EnterEditMode());
            SaveEditCommand = new AsyncRelayCommand(SaveEditAsync);
            CancelEditCommand = new RelayCommand(_ => CancelEdit());

            // Page is cached, so refresh on any stock change (sale, return, new purchase).
            eventBus.InventoryChanged += (_, _) => _dispatcherQueue.TryEnqueue(async () => await LoadAsync());

            _ = InitializeAsync();
        }

        public ObservableCollection<InvoiceGroup> InvoiceGroups { get; } = new();
        public ObservableCollection<InventoryBatch> InvoiceItems { get; } = new();
        public ObservableCollection<EditableInvoiceItem> EditableItems { get; } = new();

        public ICommand RefreshCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand EditInvoiceCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        // ---------------- list ----------------
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetProperty(ref _searchText, value)) _ = DebouncedLoadAsync(); }
        }

        /// <summary>today / week / month / all</summary>
        private string _range = "all";
        public string Range
        {
            get => _range;
            set { if (SetProperty(ref _range, value)) _ = LoadAsync(); }
        }

        private int _invoiceCount;
        public int InvoiceCount { get => _invoiceCount; private set => SetProperty(ref _invoiceCount, value); }
        private decimal _invoicesTotal;
        public decimal InvoicesTotal { get => _invoicesTotal; private set => SetProperty(ref _invoicesTotal, value); }

        private async Task InitializeAsync()
        {
            await RunAutoCleanupAsync();
            await LoadAsync();
        }

        private async Task DebouncedLoadAsync()
        {
            _searchCts?.Cancel();
            var cts = _searchCts = new CancellationTokenSource();
            try { await Task.Delay(300, cts.Token); } catch (TaskCanceledException) { return; }
            await LoadAsync();
        }

        private (DateTime? from, DateTime? to) RangeBounds()
        {
            var today = DateTime.Today;
            return Range switch
            {
                "today" => (today, today.AddDays(1)),
                "week" => (today.AddDays(-6), today.AddDays(1)),
                "month" => (new DateTime(today.Year, today.Month, 1), today.AddDays(1)),
                _ => (null, null)
            };
        }

        private async Task LoadAsync()
        {
            try
            {
                var (from, to) = RangeBounds();
                var list = await _invoiceRepo.SearchAsync(SearchText, from, to);
                int? keepId = SelectedInvoice?.Id;

                InvoiceGroups.Clear();
                foreach (var day in list.GroupBy(i => i.InvoiceDate.Date))
                    InvoiceGroups.Add(new InvoiceGroup(DayLabel(day.Key), day));

                InvoiceCount = list.Count;
                InvoicesTotal = list.Sum(i => i.TotalAmount);
                StatusMessage = list.Count == 0 ? "No invoices match." : string.Empty;

                // Clearing the list drops the selection; keep the same invoice selected after a refresh.
                if (!IsEditMode) SelectedInvoice = keepId == null ? null : list.Find(i => i.Id == keepId);
            }
            catch (Exception ex)
            {
                StatusMessage = "✘ Could not load invoices.";
                AppLogger.LogError("PurchaseHistory.Load", ex);
            }
        }

        private static string DayLabel(DateTime d) =>
            d == DateTime.Today ? $"Today · {d:d MMM}"
            : d == DateTime.Today.AddDays(-1) ? $"Yesterday · {d:d MMM}"
            : d.ToString("ddd, d MMM yyyy");

        // ---------------- selected invoice ----------------
        private PurchaseInvoice? _selectedInvoice;
        public PurchaseInvoice? SelectedInvoice
        {
            get => _selectedInvoice;
            set
            {
                if (SetProperty(ref _selectedInvoice, value))
                {
                    if (_isEditMode) CancelEdit();
                    OnPropertyChanged(nameof(HasSelectedInvoice));
                    OnPropertyChanged(nameof(DetailFacts));
                    _ = LoadInvoiceDetailsAsync(value);
                }
            }
        }
        public bool HasSelectedInvoice => _selectedInvoice != null;
        public string DetailFacts => SelectedInvoice == null ? string.Empty
            : $"{SelectedInvoice.InvoiceDate:ddd d MMM yyyy}   ·   {SelectedInvoice.SupplierName}";


        public decimal InvoiceGross => InvoiceItems.Sum(i => i.InvoiceAmountOrNet);
        public decimal InvoiceNet => InvoiceItems.Sum(i => i.PurchaseTotalPrice);
        public decimal InvoiceSaved => InvoiceGross - InvoiceNet;

        private async Task LoadInvoiceDetailsAsync(PurchaseInvoice? invoice)
        {
            InvoiceItems.Clear();
            if (invoice != null)
            {
                IsBusy = true;
                try
                {
                    foreach (var item in await _invoiceRepo.GetInvoiceItemsAsync(invoice.Id)) InvoiceItems.Add(item);
                }
                catch (Exception ex)
                {
                    AppLogger.LogError($"PurchaseHistory.LoadDetails id={invoice.Id}", ex);
                }
                finally { IsBusy = false; }
            }
            OnPropertyChanged(nameof(InvoiceGross));
            OnPropertyChanged(nameof(InvoiceNet));
            OnPropertyChanged(nameof(InvoiceSaved));
        }

        // ---------------- edit ----------------
        private bool _isEditMode;
        public bool IsEditMode
        {
            get => _isEditMode;
            set { if (SetProperty(ref _isEditMode, value)) OnPropertyChanged(nameof(IsNotEditMode)); }
        }
        public bool IsNotEditMode => !_isEditMode;
        public decimal EditTotal => EditableItems.Sum(i => i.EditTotalCost);

        private void EnterEditMode()
        {
            if (SelectedInvoice == null || InvoiceItems.Count == 0) return;

            EditableItems.Clear();
            foreach (var batch in InvoiceItems)
            {
                var editable = EditableInvoiceItem.FromBatch(batch);
                editable.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(EditableInvoiceItem.EditTotalCost))
                        OnPropertyChanged(nameof(EditTotal));
                };
                EditableItems.Add(editable);
            }

            IsEditMode = true;
            OnPropertyChanged(nameof(EditTotal));
        }

        private async Task SaveEditAsync()
        {
            if (SelectedInvoice == null || EditableItems.Count == 0) return;

            var invalid = EditableItems.FirstOrDefault(i =>
                string.IsNullOrWhiteSpace(i.EditBatchNo) || i.EditPackQuantity <= 0 || i.EditTotalCost <= 0);
            if (invalid != null)
            {
                await _dialogService.ShowMessageAsync("Validation Error", $"'{invalid.MedicineName}' has missing batch, quantity, or cost.");
                return;
            }

            IsBusy = true;
            try
            {
                if (await _invoiceRepo.UpdateInvoiceItemsAsync(SelectedInvoice.Id, EditableItems.ToList()))
                {
                    IsEditMode = false;
                    EditableItems.Clear();
                    await LoadAsync();                              // list shows the new total
                    await LoadInvoiceDetailsAsync(SelectedInvoice);
                    StatusMessage = "✔ Invoice updated. Stock adjusted.";
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Error", "Failed to update the invoice. Please try again.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"PurchaseHistory.SaveEdit id={SelectedInvoice.Id}", ex);
                await _dialogService.ShowMessageAsync("Error", "An error occurred while saving changes.");
            }
            finally { IsBusy = false; }
        }

        private void CancelEdit()
        {
            IsEditMode = false;
            EditableItems.Clear();
        }

        // ---------------- delete ----------------
        private async Task DeleteSelectedInvoiceAsync()
        {
            if (SelectedInvoice == null) return;

            var invoice = SelectedInvoice;
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Invoice",
                $"Delete invoice \"{invoice.InvoiceNo}\"?\n\nOnly the invoice record is removed. Stock is not changed.",
                "Delete",
                "Cancel");
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                if (await _invoiceRepo.DeleteAsync(invoice.Id))
                {
                    SelectedInvoice = null;
                    await LoadAsync();
                    StatusMessage = $"✔ Invoice \"{invoice.InvoiceNo}\" deleted.";
                }
                else
                {
                    await _dialogService.ShowMessageAsync("Error", "Failed to delete the invoice. Please try again.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"PurchaseHistory.Delete id={invoice.Id}", ex);
                await _dialogService.ShowMessageAsync("Error", "An error occurred while deleting the invoice.");
            }
            finally { IsBusy = false; }
        }

        /// <summary>Removes invoices whose stock is fully sold (on open; SaleRepository runs it after each sale).</summary>
        private async Task RunAutoCleanupAsync()
        {
            try
            {
                var deleted = await _invoiceRepo.CleanupFullySoldInvoicesAsync();
                if (deleted > 0) AppLogger.LogInfo($"[Invoice Auto-Cleanup] {deleted} fully-sold invoices removed.");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PurchaseHistory.AutoCleanup", ex);
            }
        }
    }

    /// <summary>One day's invoices in the list, with the day's total in the header.</summary>
    public class InvoiceGroup : List<PurchaseInvoice>
    {
        public InvoiceGroup(string label, IEnumerable<PurchaseInvoice> invoices) : base(invoices)
        {
            Label = label;
            Total = this.Sum(i => i.TotalAmount);
        }
        public string Label { get; }
        public decimal Total { get; }
    }
}
