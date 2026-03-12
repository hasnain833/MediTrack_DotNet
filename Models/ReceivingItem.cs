using System;
using System.Collections.Generic;
using DChemist.Utils;

namespace DChemist.Models
{
    /// <summary>
    /// Represents one line in the Stock-In receiving list before it is
    /// committed to inventory_batches. All quantities are in the medicine's
    /// base unit for storage purposes.
    /// </summary>
    public class ReceivingItem : ViewModelBase
    {
        // ── Medicine Identity ─────────────────────────────────────────────
        public int    MedicineId       { get; set; }
        public string MedicineName     { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;

        // ── Packaging (copied from Medicine at scan time) ─────────────────
        public string BaseUnit  { get; set; } = "unit";
        public int?   StripSize { get; set; }
        public int?   BoxSize   { get; set; }

        // ── Batch Info ───────────────────────────────────────────────────
        private string _batchNumber = string.Empty;
        public string BatchNumber
        {
            get => _batchNumber;
            set => SetProperty(ref _batchNumber, value);
        }

        private DateTime? _expiryDate;
        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        // ── Supplier ─────────────────────────────────────────────────────
        public int    SupplierId   { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        // ── Unit Selection ───────────────────────────────────────────────
        private string _selectedUnit = string.Empty;
        public string SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                if (SetProperty(ref _selectedUnit, value))
                {
                    OnPropertyChanged(nameof(ConversionFactor));
                    OnPropertyChanged(nameof(BaseUnitsToStore));
                }
            }
        }

        /// <summary>How many base units are in one of the selected unit.</summary>
        public int ConversionFactor
        {
            get
            {
                if (string.Equals(SelectedUnit, "Strip", StringComparison.OrdinalIgnoreCase)) return StripSize ?? 1;
                if (string.Equals(SelectedUnit, "Box",   StringComparison.OrdinalIgnoreCase)) return BoxSize   ?? 1;
                return 1;
            }
        }

        private int _quantity = 1;
        public int Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, value))
                    OnPropertyChanged(nameof(BaseUnitsToStore));
            }
        }

        /// <summary>Actual base units that will be stored in inventory_batches.stock_qty.</summary>
        public int BaseUnitsToStore => Quantity * ConversionFactor;

        // ── Pricing ──────────────────────────────────────────────────────
        private decimal _purchasePrice;
        public decimal PurchasePrice
        {
            get => _purchasePrice;
            set => SetProperty(ref _purchasePrice, value);
        }

        private decimal _sellingPrice;
        public decimal SellingPrice
        {
            get => _sellingPrice;
            set => SetProperty(ref _sellingPrice, value);
        }

        // ── Available units list (for ComboBox) ───────────────────────────
        public List<string> AvailableUnits
        {
            get
            {
                var list = new List<string> { Capitalize(BaseUnit) };
                if (StripSize.HasValue && StripSize.Value > 0) list.Add("Strip");
                if (BoxSize.HasValue   && BoxSize.Value   > 0) list.Add("Box");
                return list;
            }
        }

        private static string Capitalize(string s) =>
            string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s[1..];
    }
}
