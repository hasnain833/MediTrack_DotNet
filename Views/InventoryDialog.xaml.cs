using System;
using Microsoft.UI.Xaml.Controls;
using DChemist.Models;

namespace DChemist.Views
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
                    Id             = medicine.Id,
                    Name           = medicine.Name,
                    GenericName    = medicine.GenericName,
                    CategoryName   = medicine.CategoryName,
                    ManufacturerName = medicine.ManufacturerName,
                    DosageForm     = medicine.DosageForm,
                    Strength       = medicine.Strength,
                    Barcode        = medicine.Barcode,
                    SellingPrice   = medicine.SellingPrice,
                    PurchasePrice  = medicine.PurchasePrice,
                    StockQty       = medicine.StockQty,
                    ExpiryDate     = medicine.ExpiryDate,
                    SupplierName   = medicine.SupplierName,
                    GstPercent     = medicine.GstPercent
                };
                
                Title = "Edit Medicine";
                NameInput.Text          = Result.Name;

                BarcodeInput.Text       = Result.Barcode;
                SellingPriceInput.Text  = Result.SellingPrice.ToString("G");
                PurchasePriceInput.Text = Result.PurchasePrice.ToString("G");
                StockInput.Text         = Result.StockQty.ToString();
                ExpiryInput.Date        = Result.ExpiryDate;
                SupplierInput.Text      = Result.SupplierName ?? "";
                GstInput.Text           = Result.GstPercent.ToString("G");
            }
            else
            {
                Result = new Medicine { ExpiryDate = DateTime.Now.AddYears(1) };
                Title = "Add New Medicine";
                ExpiryInput.Date   = Result.ExpiryDate;
            }

            this.PrimaryButtonClick += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(NameInput.Text))
                {
                    e.Cancel = true;
                    ShowError("Medicine name is required.");
                    return;
                }

                // --- Collect values ---
                Result.Name           = NameInput.Text;
                Result.GenericName    = "";
                Result.CategoryName   = "General";
                Result.ManufacturerName = "GSK";
                Result.DosageForm     = "";
                Result.Strength       = "";
                Result.Barcode        = string.IsNullOrWhiteSpace(BarcodeInput.Text) ? null : BarcodeInput.Text.Trim();
                Result.SellingPrice   = decimal.TryParse(SellingPriceInput.Text, out var sp) ? sp : 0;
                Result.PurchasePrice  = decimal.TryParse(PurchasePriceInput.Text, out var pp) ? pp : 0;
                Result.StockQty       = int.TryParse(StockInput.Text, out var sq) ? sq : 0;
                Result.SupplierName   = SupplierInput.Text;
                Result.GstPercent     = decimal.TryParse(GstInput.Text, out var gst) ? gst : 0;
                Result.ExpiryDate     = ExpiryInput.Date?.DateTime;
            };
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text       = message;
            ErrorMessage.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
        }
    }
}
