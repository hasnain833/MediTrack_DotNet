using System.Text;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public static class ReceiptBuilder
    {
        public static string BuildReceiptString(ReceiptViewModel receipt)
        {
            var sb = new StringBuilder();
            
            // ESC/POS Initialization
            sb.Append((char)27).Append((char)64);
            
            // Header: Center justify
            sb.Append((char)27).Append((char)97).Append((char)1);
            sb.AppendLine(receipt.PharmacyName);
            sb.AppendLine(receipt.PharmacyAddress);
            sb.AppendLine(receipt.PharmacyPhone);
            sb.AppendLine("--------------------------------");
            
            // Details: Left justify
            sb.Append((char)27).Append((char)97).Append((char)0);
            sb.AppendLine($"Bill No: {receipt.BillNo}");
            sb.AppendLine($"Date:    {receipt.Date}");
            sb.AppendLine("--------------------------------");
            
            // Items
            foreach (var item in receipt.Items)
            {
                sb.AppendLine($"{item.Name}");
                sb.AppendLine($"  {item.Quantity} x {item.Price:F2}    = {item.Total:F2}");
            }
            sb.AppendLine("--------------------------------");
            
            // Totals
            sb.AppendLine($"Subtotal:       {receipt.TotalAmount:F2}");
            sb.AppendLine($"{receipt.TaxRateText,-16}{receipt.TaxAmount:F2}");
            if (receipt.DiscountAmount > 0)
                sb.AppendLine($"Discount:      -{receipt.DiscountAmount:F2}");
            
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"GRAND TOTAL:    {receipt.GrandTotal:F2}");
            sb.AppendLine("--------------------------------");
            
            // Footer: Center justify
            sb.Append((char)27).Append((char)97).Append((char)1);
            if (receipt.FbrInvoiceNo != "N/A")
            {
                sb.AppendLine("FBR INVOICE");
                sb.AppendLine(receipt.FbrInvoiceNo);
            }
            else
            {
                sb.AppendLine("INTERNAL INVOICE");
            }
            sb.AppendLine("Thank you for your visit!");
            
            // Feed and cut (GS V B 0)
            sb.Append((char)29).Append((char)86).Append((char)66).Append((char)0);

            return sb.ToString();
        }
    }
}
