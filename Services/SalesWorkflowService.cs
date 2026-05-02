using System;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Models;
using DChemist.Models.UseCases;
using DChemist.Repositories;
using DChemist.Utils;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public interface ISalesWorkflowService
    {
        Task<CompleteSaleResult> CompleteSaleAsync(CompleteSaleRequest request);
        Task<FinancialActionResult> PrintReceiptAsync(PrintReceiptRequest request);
    }

    public class SalesWorkflowService : ISalesWorkflowService
    {
        private readonly BatchRepository _batchRepository;
        private readonly CustomerRepository _customerRepository;
        private readonly SaleRepository _saleRepository;
        private readonly AuthService _authService;
        private readonly IFiscalService _fiscalService;
        private readonly IPrintService _printService;
        private readonly SettingsService _settingsService;

        public SalesWorkflowService(
            BatchRepository batchRepository,
            CustomerRepository customerRepository,
            SaleRepository saleRepository,
            AuthService authService,
            IFiscalService fiscalService,
            IPrintService printService,
            SettingsService settingsService)
        {
            _batchRepository = batchRepository;
            _customerRepository = customerRepository;
            _saleRepository = saleRepository;
            _authService = authService;
            _fiscalService = fiscalService;
            _printService = printService;
            _settingsService = settingsService;
        }

        public async Task<CompleteSaleResult> CompleteSaleAsync(CompleteSaleRequest request)
        {
            if (_authService.CurrentUser == null)
                return new CompleteSaleResult { Success = false, Message = "You are not logged in." };

            if (request.Items == null || request.Items.Count == 0)
                return new CompleteSaleResult { Success = false, Message = "No cart items to process." };

            foreach (var item in request.Items)
            {
                var totalStock = await _batchRepository.GetTotalStockAsync(item.MedicineId);
                if (totalStock < item.QuantityUnitsForStock)
                {
                    return new CompleteSaleResult
                    {
                        Success = false,
                        Message = $"Insufficient total stock for '{item.MedicineName}'. Available: {totalStock} units. Requested: {item.QuantityUnitsForStock} units."
                    };
                }
            }

            int? customerId = null;
            if (!string.IsNullOrWhiteSpace(request.CustomerName))
            {
                var customer = await _customerRepository.FindOrCreateAsync(request.CustomerName, request.CustomerPhone);
                customerId = customer?.Id;
            }

            string billNo = "BILL-" + DateTime.Now.Ticks.ToString().Substring(10);
            var items = request.Items.Select(i => new SaleItem
            {
                MedicineId = i.MedicineId,
                BatchId = i.BatchId,
                MedicineName = i.MedicineName,
                Quantity = i.QuantityUnitsForStock,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList();

            var saleId = await _saleRepository.CreateTransactionAsync(
                billNo,
                _authService.CurrentUser.Id,
                customerId,
                items,
                request.TotalAmount,
                request.TaxAmount,
                request.DiscountAmount,
                request.GrandTotal,
                false,
                null,
                null);

            // FBR integration is intentionally disabled.
            string? fbrInvNo = null;

            return new CompleteSaleResult
            {
                Success = true,
                BillNo = billNo,
                FbrInvoiceNo = fbrInvNo,
                Message = "Sale saved internally only."
            };
        }

        public async Task<FinancialActionResult> PrintReceiptAsync(PrintReceiptRequest request)
        {
            try
            {
                var receiptVm = new ReceiptViewModel
                {
                    BillNo = request.BillNo,
                    FbrInvoiceNo = request.FbrInvoiceNo,
                    CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Walk-in Customer" : request.CustomerName,
                    CustomerPhone = request.CustomerPhone,
                    TotalAmount = request.TotalAmount,
                    TaxAmount = request.TaxAmount,
                    TaxRateText = $"Tax ({request.TaxRate * 100:0.##}%):",
                    DiscountAmount = request.DiscountAmount,
                    GrandTotal = request.GrandTotal
                };

                foreach (var item in request.Items)
                {
                    receiptVm.Items.Add(new ReceiptItemViewModel
                    {
                        Name = item.MedicineName,
                        Quantity = item.QuantityForReceipt,
                        Price = item.UnitPrice
                    });
                }

                await receiptVm.LoadStoreDetailsAsync(_settingsService);

                bool isSilent = await _settingsService.IsSilentPrintEnabledAsync();
                string printerName = await _settingsService.GetPrinterNameAsync();

                if (isSilent)
                {
                    if (string.IsNullOrWhiteSpace(printerName))
                    {
                        return new FinancialActionResult
                        {
                            Success = false,
                            Message = "Silent printing is enabled, but no printer is selected."
                        };
                    }

                    bool success = await _printService.PrintReceiptSilentAsync(receiptVm, printerName);
                    if (!success)
                    {
                        return new FinancialActionResult
                        {
                            Success = false,
                            Message = $"Silent print failed for printer '{printerName}'."
                        };
                    }
                }
                else
                {
                    var receiptControl = new Views.ReceiptTemplate(receiptVm);
                    await _printService.PrintReceiptAsync(receiptControl, "Sale Receipt " + request.BillNo);
                }

                return new FinancialActionResult { Success = true, Message = "Receipt printed successfully." };
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"PrintReceiptAsync failed for bill {request.BillNo}", ex);
                return new FinancialActionResult { Success = false, Message = ex.Message };
            }
        }
    }
}
