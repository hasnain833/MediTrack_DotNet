using System;

namespace DChemist.Models
{
    public class BatchHistory
    {
        public int     Id                  { get; set; }
        public int     MedicineId          { get; set; }
        public string  MedicineName        { get; set; } = string.Empty;
        public string  OldBatchNo          { get; set; } = string.Empty;
        public string? NewBatchNo          { get; set; }
        public int?    SupplierId          { get; set; }
        public string? SupplierName        { get; set; }
        public string  InvoiceNo           { get; set; } = string.Empty;
        public DateTime InvoiceDate        { get; set; }
        public int     QuantityUnits       { get; set; }
        public decimal PurchaseTotalPrice  { get; set; }
        public decimal UnitCost            { get; set; }
        public int?    InventoryBatchId    { get; set; }
        public string  ChangeReason        { get; set; } = "Batch number updated on new purchase";
        public DateTime CreatedAt          { get; set; } = DateTime.Now;

        // UI helpers
        public string FormattedDate    => InvoiceDate.ToString("dd/MM/yyyy");
        public string FormattedCreated => CreatedAt.ToString("dd/MM/yyyy HH:mm");
        public string FormattedCost    => $"PKR {PurchaseTotalPrice:N2}";
    }
}
