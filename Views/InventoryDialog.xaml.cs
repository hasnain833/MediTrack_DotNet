using System;
using Microsoft.UI.Xaml.Controls;
using MediTrack.Models;

namespace MediTrack.Views
{
    public sealed partial class InventoryDialog : ContentDialog
    {
        public Medicine Result { get; private set; }

        public InventoryDialog(Medicine? medicine = null)
        {
            this.InitializeComponent();
            
            if (medicine != null)
            {
                Result = new Medicine
                {
                    Id = medicine.Id,
                    MedicineName = medicine.MedicineName,
                    Category = medicine.Category,
                    Price = medicine.Price,
                    StockQty = medicine.StockQty,
                    ExpiryDate = medicine.ExpiryDate,
                    Supplier = medicine.Supplier
                };
                
                Title = "Edit Medicine";
                NameInput.Text = Result.MedicineName;
                CategoryInput.Text = Result.Category;
                PriceInput.Value = (double)Result.Price;
                StockInput.Value = Result.StockQty;
                ExpiryInput.Date = Result.ExpiryDate;
                SupplierInput.Text = Result.Supplier;
            }
            else
            {
                Result = new Medicine { ExpiryDate = DateTime.Now.AddYears(1) };
                Title = "Add New Medicine";
                ExpiryInput.Date = Result.ExpiryDate;
            }

            this.PrimaryButtonClick += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(NameInput.Text))
                {
                    e.Cancel = true;
                    ErrorMessage.Text = "Medicine name is required.";
                    ErrorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                    return;
                }

                Result.MedicineName = NameInput.Text;
                Result.Category = CategoryInput.Text ?? "";
                Result.Price = (decimal)PriceInput.Value;
                Result.StockQty = (int)StockInput.Value;
                Result.ExpiryDate = ExpiryInput.Date?.DateTime ?? DateTime.Now;
                Result.Supplier = SupplierInput.Text ?? "";
            };
        }
    }
}
