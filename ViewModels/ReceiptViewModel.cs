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
        public string PharmacyName => "D. Chemist";
        public string PharmacyAddress => "Khewra Road, Choa Saidan Shah, District Chakwal";
        public string PharmacyPhone => "+92-332-8787833";
        
        public string BillNo { get; set; } = string.Empty;
        public string Date { get; set; } = DateTime.Now.ToString("dd-MMM-yyyy HH:mm");
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        
        public ObservableCollection<ReceiptItemViewModel> Items { get; } = new();
        
        public decimal TotalAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal GrandTotal { get; set; }
        
        public string? FbrInvoiceNo { get; set; }
        public BitmapImage? QrCodeImage { get; set; }

        public async Task InitializeQrCode(IFiscalService fiscalService)
        {
            var qrData = fiscalService.GenerateFiscalQrData(BillNo, GrandTotal, TaxAmount, FbrInvoiceNo ?? "PENDING");
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
