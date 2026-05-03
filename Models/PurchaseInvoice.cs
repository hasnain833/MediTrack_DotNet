using System;
using System.Collections.Generic;

namespace DChemist.Models
{
    public class PurchaseInvoice
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public int? SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Completed";
        public DateTime CreatedAt { get; set; }

        // For UI display
        public string FormattedDate => InvoiceDate.ToString("dd/MM/yyyy");
        public string FormattedAmount => $"PKR {TotalAmount:N2}";
    }
}
