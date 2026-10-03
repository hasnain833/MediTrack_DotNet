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

        private DateTime? _expiryDate;
        public DateTime? ExpiryDate { get => _expiryDate; set => SetProperty(ref _expiryDate, value); }

        // Typed expiry, e.g. "05/27" = end of May 2027 (same rules as the Items page).
        private string? _expiryText;
        public string ExpiryText
        {
            get => _expiryText ?? ExpiryDate?.ToString("MM/yy") ?? string.Empty;
            set
            {
                _expiryText = value;
                ExpiryDate = ExpiryParser.Parse(value);
                OnPropertyChanged(nameof(ExpiryText));
            }
        }
        public bool HasValidExpiry => ExpiryDate.HasValue && ExpiryDate.Value.Date > DateTime.Today;

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

        // Free quantity from the distributor (same unit as PackQuantity). Lowers the real unit cost.
        private int _bonusQuantity;
        private string _bonusQuantityText = string.Empty;
        public int BonusQuantity => _bonusQuantity;
        public string BonusQuantityText
        {
            get => _bonusQuantityText;
            set
            {
                if (SetProperty(ref _bonusQuantityText, value))
                {
                    _bonusQuantity = int.TryParse(value, out int b) && b > 0 ? b : 0;
                    Recalculate();
                }
            }
        }

        // Distributor discount % on the typed total. Ignored for Net items.
        private decimal _discountPercent;
        private string _discountPercentText = string.Empty;
        public decimal DiscountPercent => IsNet ? 0 : _discountPercent;
        public string DiscountPercentText
        {
            get => _discountPercentText;
            set
            {
                if (SetProperty(ref _discountPercentText, value))
                {
                    _discountPercent = decimal.TryParse(value, out decimal d) && d > 0 && d < 100 ? d : 0;
                    Recalculate();
                }
            }
        }

        // Net items: what was really paid (invoice may say more). Blank = same as invoice total.
        private decimal? _actualPaid;
        private string _actualPaidText = string.Empty;
        public string ActualPaidText
        {
            get => _actualPaidText;
            set
            {
                if (SetProperty(ref _actualPaidText, value))
                {
                    _actualPaid = decimal.TryParse(value.Replace(",", "").Trim(), out decimal a) && a >= 0 ? a : null;
                    Recalculate();
                }
            }
        }

        public bool IsNet { get; set; }
        public bool CanDiscount => !IsNet;

        /// <summary>Cost per unit on the previous purchase (0 = never bought). Drives the "vs last" hint.</summary>
        public decimal LastUnitCost { get; set; }
        private decimal CostChangePercent => LastUnitCost > 0 && QuantityUnits > 0 ? (UnitCost - LastUnitCost) / LastUnitCost * 100 : 0;
        public bool IsCostUp => CostChangePercent >= 3;
        public bool IsCostOk => !IsCostUp;
        public string CostTrendText =>
            LastUnitCost <= 0 || QuantityUnits <= 0 || _purchaseTotalPrice <= 0 ? string.Empty
            : Math.Abs(CostChangePercent) < 3 ? "same as last"
            : $"{(CostChangePercent > 0 ? "▲" : "▼")} {Math.Abs(CostChangePercent):0}% vs last";

        /// <summary>Total as typed from the distributor invoice, before discount.</summary>
        public decimal GrossAmount => _packPrice;

        public int PacketsPerBox { get; set; } = 1;
        public int UnitsPerPack { get; set; } = 1;

        /// <summary>Human-readable packaging rule, e.g. "10 packs/box × 10 tabs/pack = 100 tabs/box"</summary>
        public string PkgDimension =>
            PacketsPerBox > 1
                ? $"{PacketsPerBox} packs/box × {UnitsPerPack} tabs/pack = {PacketsPerBox * UnitsPerPack} tabs/box"
                : $"{UnitsPerPack} tabs/pack";

        /// <summary>Column header label: "BOX" or "TABS" depending on entry mode.</summary>
        public string QtyLabel => EntryMode == "Box" ? "QTY (BOX)" : "QTY (TABS)";

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

        public int ToUnits(int qty) => EntryMode == "Box" ? qty * PacketsPerBox * UnitsPerPack : qty;
        public int BonusUnits => ToUnits(_bonusQuantity);

        private void Recalculate()
        {
            // PackPrice is the invoice total for the paid quantity (typed directly, not per pack).
            // Real cost = actual paid (Net items) or invoice total minus discount; stock = paid qty + bonus qty.
            _purchaseTotalPrice = IsNet
                ? _actualPaid ?? _packPrice
                : Math.Round(_packPrice * (1 - DiscountPercent / 100m), 2);
            OnPropertyChanged(nameof(PurchaseTotalPrice));

            QuantityUnits = ToUnits(PackQuantity + _bonusQuantity);

            OnPropertyChanged(nameof(QuantityUnits));
            OnPropertyChanged(nameof(UnitCost));
            OnPropertyChanged(nameof(CostTrendText));
            OnPropertyChanged(nameof(IsCostUp));
            OnPropertyChanged(nameof(IsCostOk));
            OnPropertyChanged(nameof(GrossAmount));
        }
    }
}
