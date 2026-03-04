using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using MediTrack.Models;
using MediTrack.Repositories;
using MediTrack.Services;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class BillingViewModel : ViewModelBase
    {
        private readonly MedicineRepository _medicineRepository;
        private readonly SaleRepository _saleRepository;
        private readonly CustomerRepository _customerRepository;
        private readonly AuthService _authService;

        private string _searchMedicineText = string.Empty;
        private string _customerName = string.Empty;
        private string _customerPhone = string.Empty;
        private decimal _totalAmount;
        private decimal _taxAmount;
        private decimal _discountAmount;
        private decimal _grandTotal;
        private Medicine? _selectedMedicine;
        private bool _isBusy;

        public BillingViewModel(MedicineRepository medicineRepository, SaleRepository saleRepository, 
                                CustomerRepository customerRepository, AuthService authService)
        {
            _medicineRepository = medicineRepository;
            _saleRepository = saleRepository;
            _customerRepository = customerRepository;
            _authService = authService;

            CartItems = new ObservableCollection<SaleItemViewModel>();
            MedicineResults = new ObservableCollection<Medicine>();

            SearchCommand = new RelayCommand(async _ => await SearchMedicinesAsync());
            AddToCartCommand = new RelayCommand(_ => ExecuteAddToCart(), _ => SelectedMedicine != null);
            CompleteSaleCommand = new RelayCommand(async _ => await ExecuteCompleteSaleAsync(), _ => CartItems.Any());
        }

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
            set { if (SetProperty(ref _selectedMedicine, value)) ((RelayCommand)AddToCartCommand).RaiseCanExecuteChanged(); }
        }

        public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }
        public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }
        public decimal TotalAmount { get => _totalAmount; set => SetProperty(ref _totalAmount, value); }
        public decimal TaxAmount { get => _taxAmount; set => SetProperty(ref _taxAmount, value); }
        public decimal DiscountAmount { get => _discountAmount; set { if (SetProperty(ref _discountAmount, value)) UpdateTotals(); } }
        public decimal GrandTotal { get => _grandTotal; set => SetProperty(ref _grandTotal, value); }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand SearchCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand CompleteSaleCommand { get; }

        private async Task SearchMedicinesAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchMedicineText)) { MedicineResults.Clear(); return; }
            var results = await _medicineRepository.SearchAsync(SearchMedicineText);
            MedicineResults.Clear();
            foreach (var r in results) MedicineResults.Add(r);
        }

        private void ExecuteAddToCart()
        {
            if (SelectedMedicine == null) return;
            var existing = CartItems.FirstOrDefault(i => i.MedicineId == SelectedMedicine.Id);
            if (existing != null) { existing.Quantity++; }
            else
            {
                CartItems.Add(new SaleItemViewModel { 
                    MedicineId = SelectedMedicine.Id, 
                    MedicineName = SelectedMedicine.MedicineName, 
                    UnitPrice = SelectedMedicine.Price, 
                    Quantity = 1 
                });
            }
            UpdateTotals();
            ((RelayCommand)CompleteSaleCommand).RaiseCanExecuteChanged();
        }

        private void UpdateTotals()
        {
            TotalAmount = CartItems.Sum(i => i.Subtotal);
            TaxAmount = TotalAmount * 0.18m;
            GrandTotal = TotalAmount + TaxAmount - DiscountAmount;
        }

        private async Task ExecuteCompleteSaleAsync()
        {
            if (_authService.CurrentUser == null) return;
            IsBusy = true;
            try
            {
                int? customerId = null;
                if (!string.IsNullOrWhiteSpace(CustomerName))
                {
                    var customer = await _customerRepository.FindOrCreateAsync(CustomerName, CustomerPhone);
                    customerId = customer?.Id;
                }

                string billNo = "BILL-" + DateTime.Now.Ticks.ToString().Substring(10);
                var items = CartItems.Select(i => new SaleItem { 
                    InventoryId = i.MedicineId, Quantity = i.Quantity, UnitPrice = i.UnitPrice, Subtotal = i.Subtotal 
                }).ToList();

                await _saleRepository.CreateTransactionAsync(billNo, _authService.CurrentUser.Id, customerId, items, TotalAmount, TaxAmount, DiscountAmount, GrandTotal);
                
                CartItems.Clear();
                UpdateTotals();
                CustomerName = "";
                CustomerPhone = "";
                ((RelayCommand)CompleteSaleCommand).RaiseCanExecuteChanged();
            }
            catch (Exception) { /* Log error or show message */ }
            finally { IsBusy = false; }
        }
    }

    public class SaleItemViewModel : ViewModelBase
    {
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public decimal UnitPrice { get; set; }
        private int _quantity;
        public int Quantity { get => _quantity; set { if (SetProperty(ref _quantity, value)) OnPropertyChanged(nameof(Subtotal)); } }
        public decimal Subtotal => UnitPrice * Quantity;
    }
}
