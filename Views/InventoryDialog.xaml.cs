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
                    SupplierName   = medicine.SupplierName
                };
                
                Title = "Edit Medicine";
                NameInput.Text          = Result.Name;
                GenericNameInput.Text   = Result.GenericName ?? "";
                CategoryInput.Text      = Result.CategoryName ?? "";
                ManufacturerInput.Text  = Result.ManufacturerName ?? "";
                DosageFormInput.Text    = Result.DosageForm ?? "";
                StrengthInput.Text      = Result.Strength ?? "";
                BarcodeInput.Text       = Result.Barcode;
                SellingPriceInput.Value = (double)Result.SellingPrice;
                PurchasePriceInput.Value= (double)Result.PurchasePrice;
                StockInput.Value        = Result.StockQty;
                ExpiryInput.Date        = Result.ExpiryDate;
                SupplierInput.Text      = Result.SupplierName ?? "";
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
                Result.GenericName    = GenericNameInput.Text;
                Result.CategoryName   = CategoryInput.Text ?? "";
                Result.ManufacturerName = ManufacturerInput.Text ?? "";
                Result.DosageForm     = DosageFormInput.Text ?? "";
                Result.Strength       = StrengthInput.Text ?? "";
                Result.Barcode        = BarcodeInput.Text ?? "";
                Result.SellingPrice   = (decimal)SellingPriceInput.Value;
                Result.PurchasePrice  = (decimal)PurchasePriceInput.Value;
                Result.StockQty       = (int)StockInput.Value;
                Result.SupplierName   = SupplierInput.Text;
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
