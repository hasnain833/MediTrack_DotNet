using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
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

        public async Task<FbrReportResponse> ReportSaleAsync(string billNo, decimal total, decimal tax, System.Collections.Generic.List<DChemist.Models.SaleItem> items)
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
                    InvoiceNumber = "", 
                    POSID = posId,
                    USIN = billNo,
                    DateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    BuyerName = "Customer",
                    BuyerNTN = await _settings.GetPharmacyNtnAsync(),
                    TotalBillAmount = (double)total,
                    TotalTaxAmount = (double)tax,
                    TotalQuantity = items.Sum(i => i.Quantity),
                    PaymentMode = 1, // Cash
                    InvoiceType = 1, // New
                    Items = items.Select(i => new {
                        ItemCode = i.MedicineId?.ToString() ?? "000",
                        ItemName = i.MedicineName,
                        PCTCode = "3004.9099", // Default Pharma PCT Code
                        Quantity = i.Quantity,
                        TaxRate = 0.0,
                        SaleValue = (double)i.Subtotal,
                        TaxAmount = 0.0,
                        Discount = 0.0
                    }).ToArray()
                };

                string json = JsonSerializer.Serialize(payload);
                using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, apiUrl);
                request.Content = new System.Net.Http.StringContent(json, System.Text.Encoding.UTF8);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = System.Text.Json.JsonDocument.Parse(responseBody);
                    string invNo = result.RootElement.TryGetProperty("InvoiceNumber", out var invProp) ? invProp.GetString() ?? billNo : billNo;
                    
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
