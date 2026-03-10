using Microsoft.UI.Xaml.Controls;
using DChemist.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System;

namespace DChemist.Views
{
    public sealed partial class RefundDialog : ContentDialog
    {
        public List<ReturnItemModel> Result { get; private set; } = new();

        public RefundDialog(Sale sale)
        {
            this.InitializeComponent();
            var items = sale.Items.Select(i => new ReturnItemModel(i)).ToList();
            ItemsGrid.ItemsSource = items;
            
            this.PrimaryButtonClick += (s, e) =>
            {
                Result = items.Where(i => i.ReturnInputQty > 0).ToList();
                if (!Result.Any())
                {
                    e.Cancel = true;
                    ErrorMessage.Text = "Please specify at least one item to return.";
                    ErrorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                }
            };
        }
    }

    public class ReturnItemModel : SaleItem
    {
        public ReturnItemModel(SaleItem item)
        {
            Id = item.Id;
            SaleId = item.SaleId;
            MedicineId = item.MedicineId;
            BatchId = item.BatchId;
            MedicineName = item.MedicineName;
            Quantity = item.Quantity;
            ReturnedQuantity = item.ReturnedQuantity;
            UnitPrice = item.UnitPrice;
            Subtotal = item.Subtotal;
        }

        public double ReturnInputQty { get; set; } = 0;
        public double MaxReturnable => Quantity - ReturnedQuantity;
    }
}
