using System.Text;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public static class ReceiptBuilder
    {
        public static string BuildReceiptString(ReceiptViewModel receipt)
        {
            var sb = new StringBuilder();
            
            // ESC @ (Initialize)
            sb.Append((char)27).Append((char)64);
            
            // Header: Center justify
            sb.Append((char)27).Append((char)97).Append((char)1);
            
            // Pharmacy Name: Triple height + Triple width (GS ! 0x30)
            sb.Append((char)27).Append((char)33).Append((char)48); 
            sb.AppendLine(receipt.PharmacyName);
            sb.Append((char)27).Append((char)33).Append((char)0); // Reset to normal
            
            sb.AppendLine(receipt.PharmacyAddress);
            sb.AppendLine($"Phone: {receipt.PharmacyPhone}");
            sb.AppendLine($"License: {receipt.PharmacyLicense}");
            sb.AppendLine($"NTN: {receipt.PharmacyNtn}");
            
            // Body: All Left justify for consistency
            sb.Append((char)27).Append((char)97).Append((char)0);
            sb.AppendLine("--------------------------------");
            sb.AppendLine($"Bill No: {receipt.BillNo}");
            sb.AppendLine($"Date:    {receipt.Date}");
            if (!string.IsNullOrWhiteSpace(receipt.CustomerName) && receipt.CustomerName != "Walk-in Customer")
            {
                sb.AppendLine($"Customer: {receipt.CustomerName}");
                if (!string.IsNullOrWhiteSpace(receipt.CustomerPhone))
                    sb.AppendLine($"Phone:    {receipt.CustomerPhone}");
            }
            else
            {
                sb.AppendLine("Customer: Walk-in Customer");
            }
            sb.AppendLine("--------------------------------");
            
            // Items Header: Left Aligned
            sb.AppendLine("Item           Qty     Total");
            sb.AppendLine("--------------------------------");

            // Items
            foreach (var item in receipt.Items)
            {
                string name = item.Name.Length > 15 ? item.Name.Substring(0, 12) + "..." : item.Name.PadRight(15);
                string qty = item.Quantity.ToString().PadRight(6);
                string total = "PKR " + item.Total.ToString("F2");
                sb.AppendLine($"{name}{qty}{total.PadLeft(11)}");
            }
            sb.AppendLine("--------------------------------");
            
            // Totals
            sb.AppendLine($"Subtotal:           PKR {receipt.TotalAmount:F2}");
            sb.AppendLine($"{receipt.TaxRateText.Replace(":", ""),-20}PKR {receipt.TaxAmount:F2}");
            if (receipt.DiscountAmount > 0)
                sb.AppendLine($"Discount:          -PKR {receipt.DiscountAmount:F2}");
            
            sb.AppendLine("--------------------------------");
            
            // Grand Total: Centered and Big
            sb.Append((char)27).Append((char)97).Append((char)1); // Center
            sb.Append((char)27).Append((char)33).Append((char)48); // Large
            sb.AppendLine($"TOTAL: PKR {receipt.GrandTotal:F2}");
            sb.Append((char)27).Append((char)33).Append((char)0);  // Normal
            sb.AppendLine("--------------------------------");
            
            // Footer: Center justify
            sb.AppendLine("FBR SIMULATOR MODE");
            if (receipt.FbrInvoiceNo != null && receipt.FbrInvoiceNo != "N/A")
            {
                sb.AppendLine(receipt.FbrInvoiceNo);
            }
            else
            {
                sb.AppendLine("INTERNAL INVOICE");
            }
            sb.AppendLine("Thank you for your visit!");
            sb.AppendLine("(Scan QR on screen to verify)");

            // GS V B 0 (Cut)
            sb.Append((char)29).Append((char)86).Append((char)66).Append((char)0);

            return sb.ToString();
        }
    }
}
