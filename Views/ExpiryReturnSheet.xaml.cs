using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DChemist.Models;

namespace DChemist.Views
{
    public sealed partial class ExpiryReturnSheet : UserControl
    {
        private const int RowsPerPage = 30; // fits A4 with the header and the totals/signature block

        private ExpiryReturnSheet() { InitializeComponent(); }

        /// <summary>Splits one supplier's rows into A4 pages; totals and signatures go on the last page.</summary>
        public static List<UIElement> BuildPages(IReadOnlyList<ExpiringStockRow> rows, string pharmacyName, string pharmacyInfo)
        {
            for (int i = 0; i < rows.Count; i++) rows[i].No = i + 1;
            var chunks = rows.Chunk(RowsPerPage).ToList();
            var first = rows[0];
            var pages = new List<UIElement>();
            for (int p = 0; p < chunks.Count; p++)
            {
                var page = new ExpiryReturnSheet();
                page.PharmacyNameText.Text = pharmacyName;
                page.PharmacyInfoText.Text = pharmacyInfo;
                page.DateText.Text = $"Date: {DateTime.Now:dd MMM yyyy}";
                page.PageText.Text = $"Page {p + 1} of {chunks.Count}";
                page.SupplierText.Text = string.IsNullOrWhiteSpace(first.SupplierPhone) ? first.SupplierName : $"{first.SupplierName}  ·  {first.SupplierPhone}";
                page.ScopeText.Text = DChemist.Utils.ExpiryPolicy.ScopeText + "\nQty in units (tabs)";
                page.RowsList.ItemsSource = chunks[p];
                if (p == chunks.Count - 1)
                {
                    page.TotalsRow.Visibility = Visibility.Visible;
                    page.SignRow.Visibility = Visibility.Visible;
                    page.TotalsLabel.Text = $"{rows.Count} batches  ·  {rows.Sum(r => r.RemainingUnits)} units";
                    page.TotalsValue.Text = $"Total value  PKR {rows.Sum(r => r.Value):N2}";
                }
                pages.Add(page);
            }
            return pages;
        }
    }
}
