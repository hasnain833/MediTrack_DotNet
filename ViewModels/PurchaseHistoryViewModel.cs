using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class PurchaseHistoryViewModel : ViewModelBase
    {
        private readonly PurchaseInvoiceRepository _invoiceRepo;
        private PurchaseInvoice? _selectedInvoice;
        private bool _isBusy;
        private string _statusMessage = string.Empty;

        public PurchaseHistoryViewModel(PurchaseInvoiceRepository invoiceRepo)
        {
            _invoiceRepo = invoiceRepo;
            Invoices = new ObservableCollection<PurchaseInvoice>();
            InvoiceItems = new ObservableCollection<InventoryBatch>();
            
            RefreshCommand = new AsyncRelayCommand(LoadInvoicesAsync);
            
            _ = LoadInvoicesAsync();
        }

        public ObservableCollection<PurchaseInvoice> Invoices { get; }
        public ObservableCollection<InventoryBatch> InvoiceItems { get; }

        public PurchaseInvoice? SelectedInvoice
        {
            get => _selectedInvoice;
            set
            {
                if (SetProperty(ref _selectedInvoice, value))
                {
                    _ = LoadInvoiceDetailsAsync(value);
                }
            }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public ICommand RefreshCommand { get; }

        private async Task LoadInvoicesAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading invoices...";
            try
            {
                var list = await _invoiceRepo.GetAllAsync();
                Invoices.Clear();
                foreach (var item in list) Invoices.Add(item);
                
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
    }
}
