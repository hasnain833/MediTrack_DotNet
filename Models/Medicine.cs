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
        public int UnitsPerPack { get; set; } = 1;
        public int PacketsPerBox { get; set; } = 1;
        public string DefaultEntryMode { get; set; } = "Tablet";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Batch-specific properties for Inventory Page
        public int? BatchId { get; set; }
        public string? BatchEntryMode { get; set; }
        public int? BatchUnitsPerPack { get; set; }
        public int? BatchPackQuantity { get; set; }

        public string FormattedQuantity
        {
            get
            {
                if (BatchEntryMode == "Box" && PacketsPerBox > 0 && UnitsPerPack > 0)
                {
                    int unitsPerBox = PacketsPerBox * UnitsPerPack;
                    int boxes = StockQty / unitsPerBox;
                    int loose = StockQty % unitsPerBox;
                    if (loose == 0) return $"{boxes} Box";
                    return $"{boxes} Box + {loose} Tab";
                }
                return $"{StockQty} Units";
            }
        }

        private bool _isSelected;
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // ── Inline-edit state ────────────────────────────────────────────
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetProperty(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(IsNotEditing));
                }
            }
        }
        public bool IsNotEditing => !_isEditing;

        // Editable shadow copies (populated when edit starts)
        private string _editName = string.Empty;
        public string EditName { get => _editName; set => SetProperty(ref _editName, value); }

        private string _editCategoryName = string.Empty;
        public string EditCategoryName { get => _editCategoryName; set => SetProperty(ref _editCategoryName, value); }

        private string _editSellingPrice = string.Empty;
        public string EditSellingPrice { get => _editSellingPrice; set => SetProperty(ref _editSellingPrice, value); }

        private string _editBatchNo = string.Empty;
        public string EditBatchNo { get => _editBatchNo; set => SetProperty(ref _editBatchNo, value); }

        private string _editExpiryDate = string.Empty;
        public string EditExpiryDate { get => _editExpiryDate; set => SetProperty(ref _editExpiryDate, value); }

        /// <summary>Copies current values into edit fields so the user sees them pre-filled.</summary>
        public void BeginEdit()
        {
            EditName         = Name ?? string.Empty;
            EditCategoryName = CategoryName ?? string.Empty;
            EditSellingPrice = SellingPrice.ToString("G29");
            EditBatchNo      = BatchNo ?? string.Empty;
            EditExpiryDate   = ExpiryDate.HasValue ? ExpiryDate.Value.ToString("MM/yyyy") : string.Empty;
            IsEditing        = true;
        }

        /// <summary>Applies edit fields back to the model properties.</summary>
        public void CommitEdit()
        {
            Name         = EditName;
            CategoryName = EditCategoryName;
            if (decimal.TryParse(EditSellingPrice, System.Globalization.NumberStyles.Any,
                                  System.Globalization.CultureInfo.InvariantCulture, out decimal sp))
                SellingPrice = sp;
            BatchNo = EditBatchNo;
            IsEditing = false;
        }

        public void CancelEdit() => IsEditing = false;

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

