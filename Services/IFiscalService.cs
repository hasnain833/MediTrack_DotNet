using System.Threading.Tasks;

namespace DChemist.Services
{
    public interface IFiscalService
    {
        string GenerateFiscalQrData(string billNo, decimal amount, decimal tax, string fbrInvoiceNo);
        byte[] GenerateQrCodeImage(string data);
        Task<FbrReportResponse> ReportSaleAsync(string billNo, decimal total, decimal tax);
    }

    public class FbrReportResponse
    {
        public bool Success { get; set; }
        public string? InvoiceNumber { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ResponseRaw { get; set; }
    }
}
