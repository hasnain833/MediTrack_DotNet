using System;
using System.Text;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public static class ReceiptBuilder
    {
        public static string BuildReceiptString(ReceiptViewModel receipt)
        {
            var sb = new StringBuilder();
            const int width = 48; // 80mm thermal printer = 48 characters
            const string separator = "------------------------------------------------"; // 48 dashes
            
            // ESC @ (Initialize)
            sb.Append((char)27).Append((char)64);
            
            // --- HEADER (Centered) ---
            sb.Append((char)27).Append((char)97).Append((char)1); // Center
            
            // Pharmacy Name: Large Text (+ Double Height/Width)
            sb.Append((char)27).Append((char)33).Append((char)48); 
            sb.AppendLine(receipt.PharmacyName);
            sb.Append((char)27).Append((char)33).Append((char)0); // Normal size
            
            sb.AppendLine();
            sb.AppendLine(receipt.PharmacyAddress);
            sb.AppendLine($"Phone: {receipt.PharmacyPhone}");
            sb.AppendLine($"License: {receipt.PharmacyLicense}");
            sb.AppendLine($"NTN: {receipt.PharmacyNtn}");
            sb.AppendLine(separator);
            
            // --- INFO (Justified Left/Right) ---
            sb.Append((char)27).Append((char)97).Append((char)0); // Left align
            sb.AppendLine(JustifyLine("Bill No:", receipt.BillNo, width));
            sb.AppendLine(JustifyLine("Date:", receipt.Date, width));
            
            string customer = (!string.IsNullOrWhiteSpace(receipt.CustomerName) && receipt.CustomerName != "Walk-in Customer")
                ? receipt.CustomerName
                : "Walk-in Customer";
            sb.AppendLine(JustifyLine("Customer:", customer, width));
            sb.AppendLine(separator);
            
            // --- ITEMS TABLE (4 Columns for 80mm) ---
            // Item (18) + Qty (6) + Rate (10) + Total (14) = 48
            sb.AppendLine("Item".PadRight(18) + "Qty".PadLeft(6) + "Rate".PadLeft(10) + "Total".PadLeft(14));
            sb.AppendLine(separator);

            foreach (var item in receipt.Items)
            {
                string itemName = item.Name.Length > 18 ? item.Name.Substring(0, 15) + "..." : item.Name;
                string qty = item.Quantity.ToString();
                string rate = item.Price.ToString("F2");
                string total = item.Total.ToString("F2");
                
                sb.AppendLine($"{itemName.PadRight(18)}{qty.PadLeft(6)}{rate.PadLeft(10)}{total.PadLeft(14)}");
            }
            sb.AppendLine(separator);
            
            // --- TOTALS ---
            sb.AppendLine(JustifyLine("Subtotal:", $"PKR {receipt.TotalAmount:F2}", width));
            
            string taxLabel = receipt.TaxRateText.Contains(":") ? receipt.TaxRateText.Split(':')[0] : receipt.TaxRateText;
            sb.AppendLine(JustifyLine($"{taxLabel}:", $"PKR {receipt.TaxAmount:F2}", width));
            
            if (receipt.DiscountAmount > 0)
                sb.AppendLine(JustifyLine("Discount:", $"-PKR {receipt.DiscountAmount:F2}", width));
            
            sb.AppendLine(separator);
            
            // --- GRAND TOTAL ---
            sb.Append((char)27).Append((char)97).Append((char)1); // Center
            sb.Append((char)27).Append((char)33).Append((char)48); // Large
            sb.AppendLine($"TOTAL: PKR {receipt.GrandTotal:N2}");
            sb.Append((char)27).Append((char)33).Append((char)0);  // Normal
            sb.AppendLine(separator);
            
            // --- FOOTER ---
            sb.Append((char)27).Append((char)97).Append((char)1); // Center
            sb.AppendLine("For any queries, please call:");
            sb.AppendLine("+92-332-8787833, 0346-7087833");
            sb.AppendLine("------------------------------------------------");
            sb.AppendLine("Thank you for your visit!");

            // Feed and Cut
            sb.AppendLine("\n\n\n\n\n");
            sb.Append((char)29).Append((char)86).Append((char)66).Append((char)0);

            return sb.ToString();
        }

        private static string JustifyLine(string label, string value, int width)
        {
            if (label.Length + value.Length >= width)
                return label + " " + value;
            
            int spaces = width - label.Length - value.Length;
            return label + new string(' ', spaces) + value;
        }
    }
}
