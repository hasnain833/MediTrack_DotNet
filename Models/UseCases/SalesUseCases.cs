using System.Collections.Generic;

namespace DChemist.Models.UseCases
{
    public class SaleLineItemDto
    {
        public int MedicineId { get; set; }
        public int BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int QuantityForReceipt { get; set; }
        public int QuantityUnitsForStock { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
    }

    public class CompleteSaleRequest
    {
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public bool ReportToFbr { get; set; }
        public List<SaleLineItemDto> Items { get; set; } = new();
    }

    public class CompleteSaleResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? BillNo { get; set; }
        public string? FbrInvoiceNo { get; set; }
        public bool FbrFailedButSavedLocally { get; set; }
    }

    public class PrintReceiptRequest
    {
        public string BillNo { get; set; } = string.Empty;
        public string? FbrInvoiceNo { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal TaxRate { get; set; }
        public List<SaleLineItemDto> Items { get; set; } = new();
    }

    public class FinancialActionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
