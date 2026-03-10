using System;
using System.Threading.Tasks;
using QRCoder;
using DChemist.Utils;

namespace DChemist.Services
{
    public class FiscalService : IFiscalService
    {
        private readonly SettingsService _settings;
        private readonly System.Net.Http.HttpClient _httpClient;

        public FiscalService(SettingsService settings)
        {
            _settings = settings;
            _httpClient = new System.Net.Http.HttpClient();
        }

        public async Task<string> GenerateFiscalQrDataAsync(string billNo, decimal amount, decimal tax, string fbrInvoiceNo)
        {
            string posId = await _settings.GetSettingAsync("fbr_pos_id", "DChemist-001");
            return $"POSID:{posId}|USIN:{billNo}|FBR_INV:{fbrInvoiceNo}|AMT:{amount:F2}|TAX:{tax:F2}|DT:{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
        }

        public byte[] GenerateQrCodeImage(string data)
        {
            using (var qrGenerator = new QRCodeGenerator())
            using (var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.M))
            using (var qrCode = new PngByteQRCode(qrCodeData))
            {
                return qrCode.GetGraphic(15);
            }
        }

        public async Task<FbrReportResponse> ReportSaleAsync(string billNo, decimal total, decimal tax)
        {
            bool isLive = (await _settings.GetSettingAsync("fbr_is_live", "false")).ToLower() == "true";
            if (!isLive) return await RunSimulatorAsync(billNo, total, tax);

            try
            {
                string apiUrl = await _settings.GetSettingAsync("fbr_api_url", "");
                string posId = await _settings.GetSettingAsync("fbr_pos_id", "");
                string token = await _settings.GetSettingAsync("fbr_token", "");

                if (string.IsNullOrEmpty(apiUrl)) throw new Exception("FBR API URL is not configured.");

                var payload = new
                {
                    InvoiceNumber = "", // FBR will return this
                    POSID = posId,
                    USIN = billNo,
                    DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    BuyerName = "Customer",
                    BuyerNTN = "0000000-0",
                    TotalBillAmount = total,
                    TotalTaxAmount = tax,
                    TotalQuantity = 1,
                    PaymentMode = 1, // Cash
                    InvoiceType = 1, // New
                    Items = new[] {
                        new {
                            ItemCode = "MED-001",
                            ItemName = "Medicines",
                            PCTCode = "3004.9099",
                            Quantity = 1,
                            TaxRate = 0,
                            SaleValue = total,
                            TaxAmount = tax,
                            Discount = 0
                        }
                    }
                };

                var json = System.Text.Json.JsonSerializer.Serialize(payload);
                var content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                if (!string.IsNullOrEmpty(token))
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PostAsync(apiUrl, content);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = System.Text.Json.JsonDocument.Parse(responseBody);
                    string invNo = result.RootElement.GetProperty("InvoiceNumber").GetString() ?? billNo;
                    
                    return new FbrReportResponse { 
                        Success = true, 
                        InvoiceNumber = invNo, 
                        ResponseRaw = responseBody 
                    };
                }
                else
                {
                    return new FbrReportResponse { 
                        Success = false, 
                        ErrorMessage = $"FBR API Error ({response.StatusCode}): {responseBody}" 
                    };
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("FBR reporting failed", ex);
                return new FbrReportResponse { Success = false, ErrorMessage = ex.Message };
            }
        }

        private async Task<FbrReportResponse> RunSimulatorAsync(string billNo, decimal total, decimal tax)
        {
            await Task.Delay(500);
            string fbrNo = "SIM-FBR-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            return new FbrReportResponse
            {
                Success = true,
                InvoiceNumber = fbrNo,
                ResponseRaw = "{\"status\":\"simulator\", \"invoice_no\":\"" + fbrNo + "\"}"
            };
        }
    }
}
