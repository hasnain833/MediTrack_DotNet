using System;
using System.Collections.Generic;

namespace DChemist.Models
{
    public class Medicine : Utils.ViewModelBase
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? GenericName { get; set; }
        public int? CategoryId { get; set; }
        public int? ManufacturerId { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string? Barcode { get; set; }
        public decimal GstPercent { get; set; } = 0;
        public string FormattedGst => GstPercent > 0 ? $"{GstPercent:G29}%" : "0%";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        private bool _isSelected;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public string? CategoryName { get; set; }
        public string? ManufacturerName { get; set; }
        public string? SupplierName { get; set; }
        public string? BatchNo { get; set; }
        
        private decimal _sellingPrice;
        public decimal SellingPrice 
        { 
            get => _sellingPrice; 
            set { if (SetProperty(ref _sellingPrice, value)) OnPropertyChanged(nameof(FormattedPurchasePrice)); } 
        }

        private decimal _purchasePrice;
        public decimal PurchasePrice 
        { 
            get => _purchasePrice; 
            set { if (SetProperty(ref _purchasePrice, value)) OnPropertyChanged(nameof(FormattedPurchasePrice)); } 
        }

        private bool _isPurchasePriceVisible;
        public bool IsPurchasePriceVisible 
        { 
            get => _isPurchasePriceVisible; 
            set { if (SetProperty(ref _isPurchasePriceVisible, value)) OnPropertyChanged(nameof(FormattedPurchasePrice)); } 
        }

        public string FormattedPurchasePrice => IsPurchasePriceVisible ? $"PKR {PurchasePrice:N2}" : "PKR ****";

        public decimal Price { get => SellingPrice; set => SellingPrice = value; }

        public int StockQty { get; set; }
        public DateTime? ExpiryDate { get; set; }

        // Helper for UI to show either DosageForm or Strength
        public string FormattedDosage => !string.IsNullOrWhiteSpace(DosageForm) ? DosageForm : Strength ?? string.Empty;
    }
}

