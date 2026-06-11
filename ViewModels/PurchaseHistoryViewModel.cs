using System;
using System.Collections.ObjectModel;
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
    public class PurchaseHistoryViewModel : ViewModelBase
    {
        private readonly PurchaseInvoiceRepository _invoiceRepo;
        private readonly IDialogService _dialogService;
        private readonly InventoryEventBus _eventBus;
        private PurchaseInvoice? _selectedInvoice;
        private bool _isBusy;
        private string _statusMessage = string.Empty;
        private string _searchText = string.Empty;
        private DateTimeOffset? _searchDate;
        private bool _isSearchActive;
        private bool _isEditMode;
        private readonly DispatcherQueue _dispatcherQueue;

        public PurchaseHistoryViewModel(PurchaseInvoiceRepository invoiceRepo, IDialogService dialogService, InventoryEventBus eventBus)
        {
            _invoiceRepo = invoiceRepo;
            _dialogService = dialogService;
            _eventBus = eventBus;
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            Invoices = new ObservableCollection<PurchaseInvoice>();
            InvoiceItems = new ObservableCollection<InventoryBatch>();
            EditableItems = new ObservableCollection<EditableInvoiceItem>();

            RefreshCommand = new AsyncRelayCommand(LoadInvoicesAsync);
            SearchCommand = new AsyncRelayCommand(ExecuteSearchAsync);
            ClearSearchCommand = new AsyncRelayCommand(ClearSearchAsync);
            DeleteInvoiceCommand = new AsyncRelayCommand(DeleteSelectedInvoiceAsync);
            EditInvoiceCommand = new RelayCommand(_ => EnterEditMode());
            SaveEditCommand = new AsyncRelayCommand(SaveEditAsync);
            CancelEditCommand = new RelayCommand(_ => CancelEdit());

            // Subscribe to inventory changes for auto-cleanup
            _eventBus.InventoryChanged += OnInventoryChanged;

            _ = InitializeAsync();
        }

        public ObservableCollection<PurchaseInvoice> Invoices { get; }
        public ObservableCollection<InventoryBatch> InvoiceItems { get; }
        public ObservableCollection<EditableInvoiceItem> EditableItems { get; }

        public PurchaseInvoice? SelectedInvoice
        {
            get => _selectedInvoice;
            set
            {
                if (SetProperty(ref _selectedInvoice, value))
                {
                    // Auto-cancel edit mode when switching invoices
                    if (_isEditMode) CancelEdit();
                    _ = LoadInvoiceDetailsAsync(value);
                    OnPropertyChanged(nameof(HasSelectedInvoice));
                }
            }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public DateTimeOffset? SearchDate
        {
            get => _searchDate;
            set => SetProperty(ref _searchDate, value);
        }

        public bool IsSearchActive
        {
            get => _isSearchActive;
            set => SetProperty(ref _isSearchActive, value);
        }

        public bool HasSelectedInvoice => _selectedInvoice != null;

        public bool IsEditMode
        {
            get => _isEditMode;
            set
            {
                if (SetProperty(ref _isEditMode, value))
                {
                    OnPropertyChanged(nameof(IsNotEditMode));
                }
            }
        }

        public bool IsNotEditMode => !_isEditMode;

        public decimal EditTotal => EditableItems.Sum(i => i.EditTotalCost);

        public ICommand RefreshCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand DeleteInvoiceCommand { get; }
        public ICommand EditInvoiceCommand { get; }
        public ICommand SaveEditCommand { get; }
        public ICommand CancelEditCommand { get; }

        private async Task InitializeAsync()
        {
            // Run auto-cleanup first, then load
            await RunAutoCleanupAsync();
            await LoadInvoicesAsync();
        }

        private async Task LoadInvoicesAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading invoices...";
            try
            {
                var list = await _invoiceRepo.GetAllAsync();
                Invoices.Clear();
                foreach (var item in list) Invoices.Add(item);

                IsSearchActive = false;
                if (Invoices.Count == 0) StatusMessage = "No purchase invoices found.";
                else StatusMessage = $"Found {Invoices.Count} invoices.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error loading invoices.";
                AppLogger.LogError("PurchaseHistory.LoadInvoices", ex);
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteSearchAsync()
        {
            // Determine which fields to search with
            string? invoiceNoFilter = null;
            string? supplierFilter = null;
            DateTime? dateFilter = null;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                // SearchText searches both invoice number and supplier name
                invoiceNoFilter = SearchText.Trim();
                supplierFilter = SearchText.Trim();
            }

            if (SearchDate.HasValue)
            {
                dateFilter = SearchDate.Value.DateTime;
            }

            // If nothing to search, just reload all
            if (invoiceNoFilter == null && supplierFilter == null && dateFilter == null)
            {
                await LoadInvoicesAsync();
                return;
            }

            IsBusy = true;
            StatusMessage = "Searching...";
            try
            {
                // Search with invoice number OR supplier name (inclusive match)
                var byInvoice = await _invoiceRepo.SearchAsync(invoiceNoFilter, null, dateFilter);
                var bySupplier = await _invoiceRepo.SearchAsync(null, supplierFilter, dateFilter);

                // Merge results, remove duplicates
                var merged = byInvoice
                    .Union(bySupplier, new InvoiceIdComparer())
                    .OrderByDescending(i => i.InvoiceDate)
                    .ToList();

                Invoices.Clear();
                foreach (var item in merged) Invoices.Add(item);

                IsSearchActive = true;
                StatusMessage = merged.Count == 0
                    ? "No invoices matched your search."
                    : $"Found {merged.Count} matching invoices.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Search failed.";
                AppLogger.LogError("PurchaseHistory.Search", ex);
            }
            finally { IsBusy = false; }
        }

        private async Task ClearSearchAsync()
        {
            SearchText = string.Empty;
            SearchDate = null;
            IsSearchActive = false;
            await LoadInvoicesAsync();
        }

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

            // Validate
            var invalid = EditableItems.FirstOrDefault(i =>
                string.IsNullOrWhiteSpace(i.EditBatchNo) ||
                i.EditPackQuantity <= 0 ||
                i.EditTotalCost <= 0);

            if (invalid != null)
            {
                await _dialogService.ShowMessageAsync("Validation Error",
                    $"'{invalid.MedicineName}' has missing batch, quantity, or cost.");
                return;
            }

            IsBusy = true;
            try
            {
                var success = await _invoiceRepo.UpdateInvoiceItemsAsync(
                    SelectedInvoice.Id, EditableItems.ToList());

                if (success)
                {
                    IsEditMode = false;
                    // Refresh: reload invoice items and update the invoice's amount in the list
                    await LoadInvoiceDetailsAsync(SelectedInvoice);
                    SelectedInvoice.TotalAmount = EditableItems.Sum(i => i.EditTotalCost);
                    // Force UI refresh for the invoice list item
                    var idx = Invoices.IndexOf(SelectedInvoice);
                    if (idx >= 0)
                    {
                        var updated = SelectedInvoice;
                        Invoices.RemoveAt(idx);
                        Invoices.Insert(idx, updated);
                        SelectedInvoice = updated;
                    }
                    StatusMessage = "Invoice updated successfully.";
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

        private async Task DeleteSelectedInvoiceAsync()
        {
            if (SelectedInvoice == null) return;

            var invoice = SelectedInvoice;
            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Delete Invoice",
                $"Are you sure you want to delete invoice \"{invoice.InvoiceNo}\"?\n\nThis will only remove the invoice record. The medicine stock/items will NOT be affected.",
                "Delete",
                "Cancel");

            if (!confirmed) return;

            IsBusy = true;
            try
            {
                var success = await _invoiceRepo.DeleteAsync(invoice.Id);
                if (success)
                {
                    Invoices.Remove(invoice);
                    SelectedInvoice = null;
                    InvoiceItems.Clear();
                    StatusMessage = $"Invoice \"{invoice.InvoiceNo}\" deleted. {Invoices.Count} invoices remaining.";
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

        private async Task LoadInvoiceDetailsAsync(PurchaseInvoice? invoice)
        {
            InvoiceItems.Clear();
            if (invoice == null) return;

            IsBusy = true;
            try
            {
                var items = await _invoiceRepo.GetInvoiceItemsAsync(invoice.Id);
                foreach (var item in items) InvoiceItems.Add(item);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"PurchaseHistory.LoadDetails id={invoice.Id}", ex);
            }
            finally { IsBusy = false; }
        }

        /// <summary>
        /// Runs auto-cleanup silently. Called on init and after every sale.
        /// </summary>
        private async Task RunAutoCleanupAsync()
        {
            try
            {
                var deleted = await _invoiceRepo.CleanupFullySoldInvoicesAsync();
                if (deleted > 0)
                {
                    AppLogger.LogInfo($"[Invoice Auto-Cleanup] {deleted} fully-sold invoices removed.");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PurchaseHistory.AutoCleanup", ex);
            }
        }

        private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
        {
            // After stock is deducted (sale made), run auto-cleanup
            if (e.ChangeType == InventoryChangeType.StockDeducted || e.ChangeType == InventoryChangeType.StockAdjusted)
            {
                _dispatcherQueue.TryEnqueue(async () =>
                {
                    await RunAutoCleanupAsync();
                    // Refresh the list if currently viewing
                    if (IsSearchActive)
                        await ExecuteSearchAsync();
                    else
                        await LoadInvoicesAsync();
                });
            }
        }

        /// <summary>Helper comparer to de-duplicate invoices by Id during search merging.</summary>
        private class InvoiceIdComparer : System.Collections.Generic.IEqualityComparer<PurchaseInvoice>
        {
            public bool Equals(PurchaseInvoice? x, PurchaseInvoice? y) => x?.Id == y?.Id;
            public int GetHashCode(PurchaseInvoice obj) => obj.Id.GetHashCode();
        }
    }
}
