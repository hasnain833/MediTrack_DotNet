using System;
using DChemist.Utils;

namespace DChemist.Models
{
    /// <summary>
    /// Wraps an InventoryBatch for inline editing on the Invoices page.
    /// Stores original values so the user can cancel and revert.
    /// </summary>
    public class EditableInvoiceItem : ViewModelBase
    {
        public int BatchId { get; set; }
        public int MedicineId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string EntryMode { get; set; } = "Tablet";
        public int PacketsPerBox { get; set; } = 1;
        public int UnitsPerPack { get; set; } = 1;

        // --- Original values (for cancel / revert) ---
        public string OriginalBatchNo { get; set; } = string.Empty;
        public int OriginalQuantityUnits { get; set; }
        public int OriginalRemainingUnits { get; set; }
        public decimal OriginalTotalCost { get; set; }
        public int OriginalPackQuantity { get; set; }

        // --- Editable fields ---
        private string _editBatchNo = string.Empty;
        public string EditBatchNo
        {
            get => _editBatchNo;
            set => SetProperty(ref _editBatchNo, value);
        }

        private string _editQuantityText = string.Empty;
        public string EditQuantityText
        {
            get => _editQuantityText;
            set
            {
                if (SetProperty(ref _editQuantityText, value))
                {
                    OnPropertyChanged(nameof(EditTotalUnits));
                    OnPropertyChanged(nameof(EditUnitCost));
                    OnPropertyChanged(nameof(FormattedEditQuantity));
                }
            }
        }

        private string _editTotalCostText = string.Empty;
        public string EditTotalCostText
        {
            get => _editTotalCostText;
            set
            {
                if (SetProperty(ref _editTotalCostText, value))
                {
                    OnPropertyChanged(nameof(EditTotalCost));
                    OnPropertyChanged(nameof(EditUnitCost));
                }
            }
        }

        /// <summary>Parsed quantity from EditQuantityText (in entry-mode units: boxes or tablets).</summary>
        public int EditPackQuantity =>
            int.TryParse(EditQuantityText, out int q) ? q : 0;

        /// <summary>Total units derived from the entry quantity.</summary>
        public int EditTotalUnits =>
            EntryMode == "Box"
                ? EditPackQuantity * PacketsPerBox * UnitsPerPack
                : EditPackQuantity;

        /// <summary>Parsed total cost.</summary>
        public decimal EditTotalCost
        {
            get
            {
                string clean = (EditTotalCostText ?? "").Replace("PKR", "").Replace(",", "").Trim();
                return decimal.TryParse(clean, out decimal c) ? c : 0;
            }
        }

        /// <summary>Auto-calculated cost per unit.</summary>
        public decimal EditUnitCost =>
            EditTotalUnits > 0 ? EditTotalCost / EditTotalUnits : 0;

        /// <summary>Formatted display for the quantity column while editing.</summary>
        public string FormattedEditQuantity =>
            EntryMode == "Box" ? $"{EditPackQuantity} Box" : $"{EditPackQuantity} Units";

        /// <summary>
        /// Creates an EditableInvoiceItem from an existing InventoryBatch.
        /// </summary>
        public static EditableInvoiceItem FromBatch(InventoryBatch batch)
        {
            // Determine the pack-level quantity to show the user
            int packQty = batch.PackQuantity > 0
                ? batch.PackQuantity
                : (batch.EntryMode == "Box" && batch.PacketsPerBox > 0 && batch.UnitsPerPack > 0
                    ? batch.QuantityUnits / (batch.PacketsPerBox * batch.UnitsPerPack)
                    : batch.QuantityUnits);

            return new EditableInvoiceItem
            {
                BatchId = batch.Id,
                MedicineId = batch.MedicineId,
                MedicineName = batch.MedicineName ?? string.Empty,
                EntryMode = batch.EntryMode,
                PacketsPerBox = batch.PacketsPerBox > 0 ? batch.PacketsPerBox : 1,
                UnitsPerPack = batch.UnitsPerPack > 0 ? batch.UnitsPerPack : 1,

                OriginalBatchNo = batch.BatchNo,
                OriginalQuantityUnits = batch.QuantityUnits,
                OriginalRemainingUnits = batch.RemainingUnits,
                OriginalTotalCost = batch.PurchaseTotalPrice,
                OriginalPackQuantity = packQty,

                _editBatchNo = batch.BatchNo,
                _editQuantityText = packQty.ToString(),
                _editTotalCostText = batch.PurchaseTotalPrice.ToString("N2"),
            };
        }

        /// <summary>
        /// Reverts all editable fields back to original values.
        /// </summary>
        public void Revert()
        {
            EditBatchNo = OriginalBatchNo;
            EditQuantityText = OriginalPackQuantity.ToString();
            EditTotalCostText = OriginalTotalCost.ToString("N2");
        }
    }
}
