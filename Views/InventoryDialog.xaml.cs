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
                    Name = medicine.Name,
                    CategoryName = medicine.CategoryName,
                    Barcode = medicine.Barcode
                };
                
                Title = "Edit Medicine";
                NameInput.Text = Result.Name;
                CategoryInput.Text = Result.CategoryName;
                BarcodeInput.Text = Result.Barcode;
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

                Result.Name = NameInput.Text;
                Result.CategoryName = CategoryInput.Text ?? "";
                Result.Barcode = BarcodeInput.Text ?? "";
                Result.Price = (decimal)PriceInput.Value;
                Result.StockQty = (int)StockInput.Value;
                Result.ExpiryDate = ExpiryInput.Date?.DateTime;
            };
        }
    }
}
