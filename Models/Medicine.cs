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
        public string Barcode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        private bool _isSelected;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Multi-unit packaging fields
        public string BaseUnit { get; set; } = "unit";
        public int? StripSize { get; set; }
        public int? BoxSize { get; set; }

        // Joined properties for UI display
        public string? CategoryName { get; set; }
        public string? ManufacturerName { get; set; }
        public string? SupplierName { get; set; }
        
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

        // Legacy compatibility
        public decimal Price { get => SellingPrice; set => SellingPrice = value; }

        public int StockQty { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public List<(string Label, int Factor)> AvailableUnits
        {
            get
            {
                var units = new List<(string, int)>
                {
                    (Capitalize(BaseUnit), 1)
                };
                if (StripSize.HasValue && StripSize.Value > 0)
                    units.Add(("Strip", StripSize.Value));
                if (BoxSize.HasValue && BoxSize.Value > 0)
                    units.Add(("Box", BoxSize.Value));
                return units;
            }
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}

