using System;
using System.Threading.Tasks;
using QRCoder;

namespace DChemist.Services
{
    public class FiscalService : IFiscalService
    {
        private const string PosId = "D.Chemist-POS-001"; // Placeholder POS ID

        public string GenerateFiscalQrData(string billNo, decimal amount, decimal tax, string fbrInvoiceNo)
        {
            // FBR-style QR data format
            // Following FBR (IMS) POS technical specifications
            return $"POSID:{PosId}|USIN:{billNo}|FBR_INV:{fbrInvoiceNo}|AMT:{amount:F2}|TAX:{tax:F2}|DT:{DateTime.Now:yyyy-MM-dd HH:mm:ss}|MT:DChemist-v1.0";
        }

        public byte[] GenerateQrCodeImage(string data)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M))
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                // 15 pixels/module is sufficient for clear thermal printing
                return qrCode.GetGraphic(15);
            }
        }

        public async Task<FbrReportResponse> ReportSaleAsync(string billNo, decimal total, decimal tax)
        {
            // Simulating real-time reporting to FBR IMS (Invoice Management System)
            // In a real implementation, this would be an HTTP POST to FBR's API
            try
            {
                await Task.Delay(1000); // Network latency simulation
                
                // Mock success: Generating a unique FBR Invoice number
                string fbrNo = "FBR-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                
                return new FbrReportResponse
                {
                    Success = true,
                    InvoiceNumber = fbrNo,
                    ResponseRaw = "{\"status\":\"success\", \"invoice_no\":\"" + fbrNo + "\", \"timestamp\":\"" + DateTime.Now.ToString("O") + "\"}"
                };
            }
            catch (Exception ex)
            {
                return new FbrReportResponse
                {
                    Success = false,
                    ErrorMessage = "Connection timeout or invalid API credentials: " + ex.Message
                };
            }
        }
    }
}
