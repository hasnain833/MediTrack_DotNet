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

        /// <summary>
        /// Builds the bill as it stands now: returned units are taken off, fully returned lines are dropped.
        /// sales.total_amount/grand_total are already reduced by returns, so the totals match the lines.
        /// </summary>
        private sealed class CompleteToPrintRequestBuilder
        {
            public PrintReceiptRequest Build(DChemist.Models.Sale sale, string customerName, decimal taxRate)
            {
                return new PrintReceiptRequest
                {
                    BillNo = sale.BillNo + " (Reprint)",
                    SaleDate = sale.SaleDate,
                    CustomerName = customerName,
                    TotalAmount = sale.TotalAmount,
                    TaxAmount = sale.TaxAmount,
                    DiscountAmount = sale.DiscountAmount,
                    ExtraAmount = sale.ExtraAmount,
                    GrandTotal = sale.GrandTotal,
                    FbrInvoiceNo = sale.Status == "Voided" ? "VOIDED - DO NOT USE" : null,
                    TaxRate = taxRate,
                    Items = sale.Items
                        .Where(item => item.Quantity - item.ReturnedQuantity > 0)
                        .Select(item => new SaleLineItemDto
                        {
                            MedicineId = item.MedicineId ?? 0,
                            BatchId = item.BatchId ?? 0,
                            MedicineName = item.MedicineName,
                            QuantityForReceipt = item.Quantity - item.ReturnedQuantity,
                            QuantityUnitsForStock = item.Quantity - item.ReturnedQuantity,
                            UnitPrice = item.UnitPrice,
                            Subtotal = (item.Quantity - item.ReturnedQuantity) * item.UnitPrice
                        }).ToList()
                };
            }
        }
    }
}
