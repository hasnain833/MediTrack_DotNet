using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public static class ReceiptBuilder
    {
        private const int Width = 48; // 80mm thermal printer = 48 characters
        private static readonly string Thin = new('-', Width);
        private static readonly string Thick = new('=', Width);

        // ESC/POS
        private const string Left = "\x1B" + "a\x00", Center = "\x1B" + "a\x01";
        private const string Large = "\x1B" + "!\x30", Bold = "\x1B" + "!\x08", Normal = "\x1B" + "!\x00";
        private const string Cut = "\x1D" + "VB\x00";

        // Item table: # (3) + Item (21) + Qty (5) + Rate (9) + Amount (10) = 48
        private const int NameWidth = 21;

        public static string BuildReceiptString(ReceiptViewModel receipt)
        {
            var sb = new StringBuilder();

            // --- HEADER ---
            sb.Append(Center).Append(Large).AppendLine(receipt.PharmacyName).Append(Normal);
            foreach (var line in Wrap(receipt.PharmacyAddress, Width)) sb.AppendLine(line);
            if (!string.IsNullOrWhiteSpace(receipt.PharmacyPhone)) sb.AppendLine($"Ph: {receipt.PharmacyPhone}");
            var ids = string.Join("   ", new[]
            {
                string.IsNullOrWhiteSpace(receipt.PharmacyLicense) ? null : $"Lic: {receipt.PharmacyLicense}",
                string.IsNullOrWhiteSpace(receipt.PharmacyNtn) ? null : $"NTN: {receipt.PharmacyNtn}"
            }.Where(s => s != null));
            if (ids.Length > 0) sb.AppendLine(ids);
            sb.AppendLine(Thick);

            // --- BILL INFO ---
            sb.Append(Left);
            sb.AppendLine(Justify($"Bill: {receipt.BillNo}", receipt.Date));
            bool walkIn = string.IsNullOrWhiteSpace(receipt.CustomerName) || receipt.CustomerName == "Walk-in Customer";
            if (!walkIn || !string.IsNullOrWhiteSpace(receipt.CustomerPhone))
                sb.AppendLine(Justify($"Customer: {(walkIn ? "Walk-in" : receipt.CustomerName)}",
                    string.IsNullOrWhiteSpace(receipt.CustomerPhone) ? "" : $"Ph: {receipt.CustomerPhone}"));
            sb.AppendLine(Thin);

            // --- ITEMS ---
            sb.Append(Bold).AppendLine(Row("#", "Item", "Qty", "Rate", "Amount")).Append(Normal);
            sb.AppendLine(Thin);
            int n = 0;
            foreach (var item in receipt.Items)
            {
                var name = Wrap(item.Name, NameWidth);
                sb.AppendLine(Row((++n).ToString(), name[0], item.Quantity.ToString(), item.Price.ToString("N2"), item.Total.ToString("N2")));
                foreach (var rest in name.Skip(1)) sb.AppendLine("   " + rest);
            }
            sb.AppendLine(Thin);

            // --- TOTALS ---
            sb.AppendLine(Justify($"Items: {receipt.Items.Count}", $"Qty: {receipt.Items.Sum(i => i.Quantity)}"));
            sb.AppendLine(Justify("Subtotal", receipt.TotalAmount.ToString("N2")));
            if (receipt.TaxAmount > 0)
                sb.AppendLine(Justify(receipt.TaxRateText.TrimEnd(':'), receipt.TaxAmount.ToString("N2")));
            if (receipt.DiscountAmount > 0)
                sb.AppendLine(Justify("Discount", "-" + receipt.DiscountAmount.ToString("N2")));
            sb.AppendLine(Thick);
            sb.Append(Center).Append(Large).AppendLine($"TOTAL Rs {receipt.GrandTotal:N2}").Append(Normal);
            sb.AppendLine(Thick);

            if (receipt.CashReceived is decimal cash)
            {
                sb.Append(Left);
                sb.AppendLine(Justify("Cash", cash.ToString("N2")));
                sb.AppendLine(Justify("Change", receipt.Change.ToString("N2")));
                sb.AppendLine(Thin);
            }

            // --- FOOTER ---
            sb.Append(Center);
            if (receipt.DiscountAmount > 0) sb.Append(Bold).AppendLine($"You saved Rs {receipt.DiscountAmount:N2}").Append(Normal);
            if (!string.IsNullOrWhiteSpace(receipt.FbrInvoiceNo)) sb.AppendLine(receipt.FbrInvoiceNo);
            foreach (var line in (receipt.ReceiptFooter ?? "").Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None)) // WinUI TextBox uses \r
                foreach (var w in Wrap(line.Trim(), Width)) sb.AppendLine(w);
            if (!string.IsNullOrWhiteSpace(receipt.PharmacyPhone)) sb.AppendLine($"For queries call: {receipt.PharmacyPhone}");

            // Feed and cut
            sb.Append('\n', 6).Append(Cut);
            return sb.ToString();
        }

        private static string Row(string no, string name, string qty, string rate, string amount) =>
            no.PadRight(3) + name.PadRight(NameWidth) + qty.PadLeft(5) + rate.PadLeft(9) + amount.PadLeft(10);

        private static string Justify(string left, string right) =>
            left.Length + right.Length >= Width ? left + " " + right : left + new string(' ', Width - left.Length - right.Length) + right;

        /// <summary>Word-wraps to <paramref name="width"/>; words longer than a line are split. Always returns at least one line.</summary>
        internal static List<string> Wrap(string? text, int width)
        {
            var lines = new List<string>();
            var cur = new StringBuilder();
            foreach (var word in (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var w = word;
                while (w.Length > width)
                {
                    if (cur.Length > 0) { lines.Add(cur.ToString()); cur.Clear(); }
                    lines.Add(w[..width]);
                    w = w[width..];
                }
                if (cur.Length > 0 && cur.Length + 1 + w.Length > width) { lines.Add(cur.ToString()); cur.Clear(); }
                if (cur.Length > 0) cur.Append(' ');
                cur.Append(w);
            }
            if (cur.Length > 0 || lines.Count == 0) lines.Add(cur.ToString());
            return lines;
        }
    }
}
