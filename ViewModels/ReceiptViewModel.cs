using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;
using DChemist.Models;
using DChemist.Services;
using DChemist.Utils;

namespace DChemist.ViewModels
{
    public class ReceiptViewModel : ViewModelBase
    {
        public string PharmacyName { get; set; } = "D. Chemist";
        public string PharmacyAddress { get; set; } = "Khewra Road, Choa Saidan Shah, District Chakwal";
        public string PharmacyPhone { get; set; } = "+92-332-8787833";
        public string PharmacyLicense { get; set; } = "01-372-0011-134212M";
        public string PharmacyNtn { get; set; } = "I736466-5";

        public async Task LoadStoreDetailsAsync(SettingsService settings)
        {
            PharmacyName = await settings.GetPharmacyNameAsync();
            PharmacyAddress = await settings.GetPharmacyAddressAsync();
            PharmacyPhone = await settings.GetPharmacyPhoneAsync();
            PharmacyLicense = await settings.GetPharmacyLicenseAsync();
            PharmacyNtn = await settings.GetPharmacyNtnAsync();
        }
        
        public string BillNo { get; set; } = string.Empty;
        public string Date { get; set; } = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        
        public ObservableCollection<ReceiptItemViewModel> Items { get; } = new();
        
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public string TaxRateText { get; set; } = "Tax:";
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        
        public string? FbrInvoiceNo { get; set; }
        public BitmapImage? QrCodeImage { get; set; }

        public async Task InitializeQrCode(IFiscalService fiscalService)
        {
            var qrData = await fiscalService.GenerateFiscalQrDataAsync(BillNo, GrandTotal, TaxAmount, FbrInvoiceNo ?? "PENDING");
            var qrBytes = fiscalService.GenerateQrCodeImage(qrData);

            using (var ms = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(ms.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(qrBytes);
                    await writer.StoreAsync();
                }

                var image = new BitmapImage();
                await image.SetSourceAsync(ms);
                QrCodeImage = image;
                OnPropertyChanged(nameof(QrCodeImage));
            }
        }
    }

    public class ReceiptItemViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Total => Quantity * Price;
    }
}
