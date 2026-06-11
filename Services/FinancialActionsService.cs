using System;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Models.UseCases;
using DChemist.Repositories;

namespace DChemist.Services
{
    public interface IFinancialActionsService
    {
        Task<FinancialActionResult> VoidSaleAsync(string billNo, int currentUserId);
        Task<FinancialActionResult> ReturnItemAsync(int saleItemId, int returnQty, int currentUserId);
        Task<FinancialActionResult> ReturnCompleteBillAsync(string billNo, int currentUserId);
        Task<FinancialActionResult> ReprintReceiptAsync(string billNo, string customerName);
    }

    public class FinancialActionsService : IFinancialActionsService
    {
        private readonly SaleRepository _saleRepo;
        private readonly ISalesWorkflowService _salesWorkflow;
        private readonly SettingsService _settingsService;

        public FinancialActionsService(
            SaleRepository saleRepo,
            ISalesWorkflowService salesWorkflow,
            SettingsService settingsService)
        {
            _saleRepo = saleRepo;
            _salesWorkflow = salesWorkflow;
            _settingsService = settingsService;
        }

        public async Task<FinancialActionResult> VoidSaleAsync(string billNo, int currentUserId)
        {
            try
            {
                await _saleRepo.VoidSaleAsync(billNo, currentUserId);
                return new FinancialActionResult { Success = true, Message = "Sale has been voided successfully." };
            }
            catch (Exception ex)
            {
                return new FinancialActionResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<FinancialActionResult> ReturnItemAsync(int saleItemId, int returnQty, int currentUserId)
        {
            try
            {
                await _saleRepo.ProcessReturnAsync(saleItemId, returnQty, currentUserId);
                return new FinancialActionResult { Success = true, Message = "Item returned and stock restored." };
            }
            catch (Exception ex)
            {
                return new FinancialActionResult { Success = false, Message = ex.Message };
            }
        }

        public async Task<FinancialActionResult> ReturnCompleteBillAsync(string billNo, int currentUserId)
        {
            try
            {
                var fullSale = await _saleRepo.GetSaleWithItemsAsync(billNo);
                if (fullSale == null)
                    return new FinancialActionResult { Success = false, Message = "Could not find the sale." };

                if (fullSale.Status == "Voided")
                    return new FinancialActionResult { Success = false, Message = "This sale is already voided." };

                int totalItemsReturned = 0;
                int totalUnitsReturned = 0;

                foreach (var item in fullSale.Items)
                {
                    int remaining = item.Quantity - item.ReturnedQuantity;
                    if (remaining <= 0) continue;

                    await _saleRepo.ProcessReturnAsync(item.Id, remaining, currentUserId);
                    totalItemsReturned++;
                    totalUnitsReturned += remaining;
                }

                if (totalItemsReturned == 0)
                    return new FinancialActionResult { Success = false, Message = "All items in this bill have already been returned." };

                return new FinancialActionResult
                {
                    Success = true,
                    Message = $"Complete bill returned successfully.\n{totalItemsReturned} item(s), {totalUnitsReturned} unit(s) returned. Stock has been restored."
                };
            }
            catch (Exception ex)
            {
                return new FinancialActionResult { Success = false, Message = $"Return failed: {ex.Message}" };
            }
        }

        public async Task<FinancialActionResult> ReprintReceiptAsync(string billNo, string customerName)
        {
            try
            {
                var fullSale = await _saleRepo.GetSaleWithItemsAsync(billNo);
                if (fullSale == null)
                {
                    return new FinancialActionResult { Success = false, Message = "Could not retrieve full sale details." };
                }

                var taxRate = await _settingsService.GetTaxRateAsync();
                var printResult = await _salesWorkflow.PrintReceiptAsync(new CompleteToPrintRequestBuilder().Build(fullSale, customerName, taxRate));
                return printResult.Success
                    ? new FinancialActionResult { Success = true, Message = "Receipt sent to printer." }
                    : printResult;
            }
            catch (Exception ex)
            {
                return new FinancialActionResult { Success = false, Message = ex.Message };
            }
        }

        private sealed class CompleteToPrintRequestBuilder
        {
            public PrintReceiptRequest Build(DChemist.Models.Sale sale, string customerName, decimal taxRate)
            {
                return new PrintReceiptRequest
                {
                    BillNo = sale.BillNo,
                    CustomerName = customerName,
                    TotalAmount = sale.TotalAmount,
                    TaxAmount = sale.TaxAmount,
                    DiscountAmount = sale.DiscountAmount,
                    GrandTotal = sale.GrandTotal,
                    FbrInvoiceNo = sale.Status == "Voided" ? "VOIDED - DO NOT USE" : "SIM-FBR-" + sale.BillNo,
                    TaxRate = taxRate,
                    Items = sale.Items.Select(item => new SaleLineItemDto
                    {
                        MedicineId = item.MedicineId ?? 0,
                        BatchId = item.BatchId ?? 0,
                        MedicineName = item.MedicineName,
                        QuantityForReceipt = item.Quantity,
                        QuantityUnitsForStock = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        Subtotal = item.Subtotal
                    }).ToList()
                };
            }
        }
    }
}
