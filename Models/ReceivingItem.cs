using System;
using DChemist.Utils;

namespace DChemist.Models
{
    public class ReceivingItem : ViewModelBase
    {
        public int    MedicineId       { get; set; }
        public string MedicineName     { get; set; } = string.Empty;
        
        private string _batchNo = "Standard";
        public string BatchNo { get => _batchNo; set => SetProperty(ref _batchNo, value); }

        private DateTime? _expiryDate = DateTime.Now.AddYears(1); 
        public DateTime? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }

        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime? InvoiceDate { get; set; }

        private int _packQuantity;
        public int PackQuantity 
        { 
            get => _packQuantity; 
            set { if (SetProperty(ref _packQuantity, value)) { _packQuantityText = value.ToString(); OnPropertyChanged(nameof(PackQuantityText)); Recalculate(); } } 
        }

        private string _packQuantityText = "0";
        public string PackQuantityText
        {
            get => _packQuantityText;
            set
            {
                if (SetProperty(ref _packQuantityText, value))
                {
                    if (int.TryParse(value, out int result)) { _packQuantity = result; Recalculate(); }
                }
            }
        }

        private decimal _packPrice;
        public decimal PackPrice
        {
            get => _packPrice;
            set { if (SetProperty(ref _packPrice, value)) { _packPriceText = value.ToString("N2"); OnPropertyChanged(nameof(PackPriceText)); Recalculate(); } }
        }

        private string _packPriceText = "0.00";
        public string PackPriceText
        {
            get => _packPriceText;
            set
            {
                if (SetProperty(ref _packPriceText, value))
                {
                    string clean = value.Replace("PKR", "").Replace(",", "").Trim();
                    if (decimal.TryParse(clean, out decimal result)) { _packPrice = result; Recalculate(); }
                }
            }
        }

        public int PacketsPerBox { get; set; } = 1;
        public int UnitsPerPack { get; set; } = 1;

        private int _quantityUnits;
        public int QuantityUnits
        {
            get => _quantityUnits;
            set { if (SetProperty(ref _quantityUnits, value)) { OnPropertyChanged(nameof(UnitCost)); OnPropertyChanged(nameof(SellingPricePerUnit)); } }
        }

        private decimal _purchaseTotalPrice;
        public decimal PurchaseTotalPrice
        {
            get => _purchaseTotalPrice;
            set { if (SetProperty(ref _purchaseTotalPrice, value)) { OnPropertyChanged(nameof(UnitCost)); } }
        }

        private decimal _unitCost;
        public decimal UnitCost 
        { 
            get => _quantityUnits > 0 ? _purchaseTotalPrice / _quantityUnits : _unitCost; 
            set => SetProperty(ref _unitCost, value); 
        }

        private decimal _sellingPricePerUnit;
        public decimal SellingPricePerUnit
        {
            get => _sellingPricePerUnit;
            set { if (SetProperty(ref _sellingPricePerUnit, value)) OnPropertyChanged(nameof(TotalSellingPrice)); }
        }

        public decimal TotalSellingPrice
        {
            get => SellingPricePerUnit * QuantityUnits;
            set { if (QuantityUnits > 0) SellingPricePerUnit = value / QuantityUnits; }
        }

        public string EntryMode { get; set; } = "Tablet";

        private void Recalculate()
        {
            _purchaseTotalPrice = PackQuantity * PackPrice;
            OnPropertyChanged(nameof(PurchaseTotalPrice));

            if (EntryMode == "Box")
                QuantityUnits = PackQuantity * PacketsPerBox * UnitsPerPack;
            else
                QuantityUnits = PackQuantity;

            OnPropertyChanged(nameof(QuantityUnits));
            OnPropertyChanged(nameof(UnitCost));
        }
    }
}
