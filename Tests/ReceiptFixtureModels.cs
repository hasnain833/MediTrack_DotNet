// Data-only fixture for the production thermal builder, without loading WinUI.
namespace DChemist.ViewModels;

public class ReceiptViewModel
{
    public string PharmacyName { get; set; } = "Test pharmacy";
    public string PharmacyAddress { get; set; } = "Test address";
    public string PharmacyPhone { get; set; } = "";
    public string PharmacyLicense { get; set; } = "";
    public string PharmacyNtn { get; set; } = "";
    public string BillNo { get; set; } = "TEST";
    public string Date { get; set; } = "10-Oct-2026";
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public List<ReceiptItemViewModel> Items { get; } = new();
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string TaxRateText { get; set; } = "Tax:";
    public decimal DiscountAmount { get; set; }
    public decimal ExtraAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public decimal? CashReceived { get; set; }
    public decimal Change => CashReceived is decimal cash ? Math.Max(0, cash - Math.Round(GrandTotal)) : 0;
    public string ReceiptFooter { get; set; } = "Thank you";
    public string? FbrInvoiceNo { get; set; }
}

public class ReceiptItemViewModel
{
    public string Name { get; set; } = "Test medicine";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal Total => Quantity * Price;
}
