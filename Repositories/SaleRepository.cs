using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Services;
using DChemist.Utils;
using Dapper;
using Npgsql;

namespace DChemist.Repositories
{
    public class SaleRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;
        private readonly InventoryEventBus _eventBus;
        private readonly AuditRepository _auditRepo;

        public SaleRepository(DatabaseService db, AuthorizationService auth, InventoryEventBus eventBus, AuditRepository auditRepo)
        {
            _db = db;
            _auth = auth;
            _eventBus = eventBus;
            _auditRepo = auditRepo;
        }

        public async Task<int> CreateTransactionAsync(string billNo, int userId, int? customerId, List<SaleItem> items,
            decimal total, decimal tax, decimal discount, decimal grandTotal, bool fbrReported = false, string? fbrInvoiceNo = null, string? fbrResponse = null)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // -- Step 1: Pre-validation (Stock Check) --
                foreach (var item in items)
                {
                    if (!item.MedicineId.HasValue) continue;

                    const string totalStockQuery = @"
                        SELECT COALESCE(SUM(remaining_units), 0)
                        FROM inventory_batches
                        WHERE medicine_id = @medId AND remaining_units > 0";
                    
                    var totalAvailable = await connection.ExecuteScalarAsync<int>(totalStockQuery, new { medId = item.MedicineId.Value }, transaction);

                    if (totalAvailable < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{item.MedicineName}'. " +
                            $"Requested: {item.Quantity} unit(s). Available: {totalAvailable}.");
                    }
                }

                // -- Step 2: Insert Sale Record --
                const string saleQuery = @"
                    INSERT INTO sales (bill_no, customer_id, user_id, total_amount, tax_amount, discount_amount, grand_total, fbr_reported, fbr_invoice_no, fbr_response, sale_date)
                    VALUES (@billNo, @customerId, @userId, @total, @tax, @discount, @grandTotal, @fbrReported, @fbrInvoiceNo, @fbrResponse, CURRENT_TIMESTAMP)
                    RETURNING id;";

                int saleId = await connection.ExecuteScalarAsync<int>(saleQuery, new 
                { 
                    billNo, customerId, userId, total, tax, discount, grandTotal, fbrReported, fbrInvoiceNo, fbrResponse 
                }, transaction);

                // -- Step 3: Insert Sale Items & Deduct Stock (FIFO) --
                foreach (var item in items)
                {
                    const string itemQuery = @"
                        INSERT INTO sale_items (sale_id, medicine_id, batch_id, quantity, unit_price, subtotal)
                        VALUES (@saleId, @medicineId, @batchId, @quantity, @unitPrice, @subtotal)";

                    await connection.ExecuteAsync(itemQuery, new 
                    { 
                        saleId, 
                        medicineId = item.MedicineId,
                        batchId = item.BatchId,
                        quantity = item.Quantity,
                        unitPrice = item.UnitPrice,
                        subtotal = item.Subtotal
                    }, transaction);

                    if (item.MedicineId.HasValue)
                    {
                        const string getBatchesQuery = @"
                            SELECT id, remaining_units 
                            FROM inventory_batches 
                            WHERE medicine_id = @medId AND remaining_units > 0 
                            ORDER BY expiry_date ASC, created_at ASC 
                            FOR UPDATE";
                        
                        var batches = await connection.QueryAsync<(int Id, int RemainingUnits)>(getBatchesQuery, new { medId = item.MedicineId.Value }, transaction);
                        
                        int remainingToDeduct = item.Quantity;
                        foreach (var batch in batches)
                        {
                            if (remainingToDeduct <= 0) break;

                            int deductFromBatch = Math.Min(remainingToDeduct, batch.RemainingUnits);
                            
                            const string updateQuery = "UPDATE inventory_batches SET remaining_units = remaining_units - @qty WHERE id = @id";
                            await connection.ExecuteAsync(updateQuery, new { qty = deductFromBatch, id = batch.Id }, transaction);
                            
                            remainingToDeduct -= deductFromBatch;
                        }

                        if (remainingToDeduct > 0)
                        {
                            throw new InvalidOperationException($"Insufficient total stock for '{item.MedicineName}' during final processing.");
                        }
                    }
                }

                await transaction.CommitAsync();

                _eventBus.Publish(InventoryChangeType.StockDeducted);
                await _auditRepo.InsertLogAsync(userId, "Sale Created", $"Bill No: {billNo}, Total: {grandTotal:F2}");
                
                return saleId;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("SaleRepository.CreateTransactionAsync failed", ex);
                throw;
            }
        }

        public async Task UpdateFbrStatusAsync(int saleId, string? fbrInvoiceNo, string? fbrResponse)
        {
            _auth.EnforceAdmin();
            const string query = "UPDATE sales SET fbr_reported = true, fbr_invoice_no = @fbrInvoiceNo, fbr_response = @fbrResponse WHERE id = @saleId";
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync(query, new { saleId, fbrInvoiceNo, fbrResponse });
        }

        public async Task<List<SaleSummary>> GetAllSummariesAsync(int limit = 50)
        {
            const string query = @"
                SELECT 
                    s.bill_no as BillNo, 
                    COALESCE(c.customer_name, 'Walking Customer') as Customer, 
                    s.grand_total as Amount, 
                    to_char(s.sale_date, 'DD Mon YYYY HH24:MI') as Date,
                    s.status as Status,
                    s.fbr_reported as FbrReported
                FROM sales s
                LEFT JOIN customers c ON s.customer_id = c.id
                ORDER BY s.sale_date DESC
                LIMIT @limit";
            
            using var conn = _db.GetConnection();
            var summaries = await conn.QueryAsync<SaleSummary>(query, new { limit });
            return summaries.ToList();
        }

        public async Task<List<SaleSummary>> SearchInvoicesAsync(string? billNo, DateTime? date, string? customer)
        {
            var query = @"
                SELECT 
                    s.bill_no as BillNo, 
                    COALESCE(c.customer_name, 'Walking Customer') as Customer, 
                    s.grand_total as Amount, 
                    to_char(s.sale_date, 'DD Mon YYYY HH24:MI') as Date,
                    s.status as Status,
                    s.fbr_reported as FbrReported
                FROM sales s
                LEFT JOIN customers c ON s.customer_id = c.id
                WHERE 1=1";
            
            var parameters = new DynamicParameters();
            if (!string.IsNullOrWhiteSpace(billNo))
            {
                query += " AND s.bill_no ILIKE @billNo";
                parameters.Add("billNo", $"%{billNo}%");
            }
            if (date.HasValue)
            {
                query += " AND s.sale_date::date = @date";
                parameters.Add("date", date.Value.Date);
            }
            if (!string.IsNullOrWhiteSpace(customer))
            {
                query += " AND c.customer_name ILIKE @customer";
                parameters.Add("customer", $"%{customer}%");
            }
            
            query += " ORDER BY s.sale_date DESC LIMIT 100";
            
            using var conn = _db.GetConnection();
            var results = await conn.QueryAsync<SaleSummary>(query, parameters);
            return results.ToList();
        }

        public async Task<decimal> GetRevenueTotalAsync(DateTime start, DateTime end)
        {
            const string query = "SELECT COALESCE(SUM(grand_total), 0) FROM sales WHERE sale_date >= @start AND sale_date <= @end AND status = 'Completed'";
            using var conn = _db.GetConnection();
            return await conn.ExecuteScalarAsync<decimal>(query, new { start, end });
        }

        public async Task<Sale?> GetSaleWithItemsAsync(string billNo)
        {
            const string saleQuery = @"
                SELECT 
                    id, bill_no as BillNo, user_id as UserId, customer_id as CustomerId, 
                    total_amount as TotalAmount, tax_amount as TaxAmount, 
                    discount_amount as DiscountAmount, grand_total as GrandTotal, 
                    sale_date as SaleDate, status as Status
                FROM sales WHERE bill_no = @billNo";
            
            using var conn = _db.GetConnection();
            var sale = await conn.QuerySingleOrDefaultAsync<Sale>(saleQuery, new { billNo });

            if (sale != null)
            {
                const string itemsQuery = @"
                    SELECT 
                        si.id, si.sale_id as SaleId, si.medicine_id as MedicineId, 
                        si.batch_id as BatchId, si.quantity, si.returned_qty as ReturnedQuantity, 
                        si.unit_price as UnitPrice, si.subtotal, 
                        m.name as MedicineName 
                    FROM sale_items si 
                    LEFT JOIN medicines m ON si.medicine_id = m.id 
                    WHERE si.sale_id = @saleId";
                
                var items = await conn.QueryAsync<SaleItem>(itemsQuery, new { saleId = sale.Id });
                sale.Items = items.ToList();
            }

            return sale;
        }

        public async Task VoidSaleAsync(string billNo, int currentUserId)
        {
            _auth.EnforceAdmin();
            
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var sale = await GetSaleWithItemsAsync(billNo);
                if (sale == null) throw new InvalidOperationException("Sale not found.");
                if (sale.Status == "Voided") throw new InvalidOperationException("Sale is already voided.");

                // 1. Update Sale Status
                await connection.ExecuteAsync("UPDATE sales SET status = 'Voided' WHERE id = @id", new { id = sale.Id }, transaction);

                // 2. Restore Inventory Stock
                foreach (var item in sale.Items)
                {
                    if (item.BatchId.HasValue)
                    {
                        await connection.ExecuteAsync("UPDATE inventory_batches SET remaining_units = remaining_units + @qty WHERE id = @batchId", 
                            new { qty = item.Quantity, batchId = item.BatchId.Value }, transaction);
                    }
                    else if (item.MedicineId.HasValue)
                    {
                        const string restoreFallbackQuery = @"
                            UPDATE inventory_batches 
                            SET remaining_units = remaining_units + @qty 
                            WHERE id = (
                                SELECT id FROM inventory_batches 
                                WHERE medicine_id = @medId 
                                ORDER BY expiry_date DESC LIMIT 1
                            )";
                        await connection.ExecuteAsync(restoreFallbackQuery, new { qty = item.Quantity, medId = item.MedicineId.Value }, transaction);
                    }
                }

                await transaction.CommitAsync();

                _eventBus.Publish(InventoryChangeType.StockAdjusted);
                await _auditRepo.InsertLogAsync(currentUserId, "Sale Voided", $"Bill No: {billNo} voided. Stock restored.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"Failed to void sale {billNo}", ex);
                throw;
            }
        }

        public async Task ProcessReturnAsync(int saleItemId, int returnQty, int currentUserId)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Fetch item details
                const string itemQuery = "SELECT sale_id, medicine_id, batch_id, quantity, returned_qty as ReturnedQuantity, unit_price FROM sale_items WHERE id = @id FOR UPDATE";
                var item = await connection.QuerySingleOrDefaultAsync<dynamic>(itemQuery, new { id = saleItemId }, transaction);
                
                if (item == null) throw new Exception("Sale item not found.");
                
                if (item.returnedquantity + returnQty > item.quantity)
                    throw new InvalidOperationException("Return quantity exceeds remaining sold quantity.");

                // 2. Update sale_items
                await connection.ExecuteAsync("UPDATE sale_items SET returned_qty = returned_qty + @returnQty WHERE id = @saleItemId", 
                    new { returnQty, saleItemId }, transaction);

                // 3. Restore stock
                await connection.ExecuteAsync("UPDATE inventory_batches SET remaining_units = remaining_units + @returnQty WHERE id = @batchId", 
                    new { returnQty, batchId = (int)item.batch_id }, transaction);

                // 4. Recalculate Sale Totals (Financial Adjustment)
                decimal deduction = returnQty * (decimal)item.unit_price;
                await connection.ExecuteAsync(@"
                    UPDATE sales 
                    SET total_amount = total_amount - @deduction,
                        grand_total = grand_total - @deduction,
                        status = 'Returned'
                    WHERE id = @saleId", 
                    new { deduction, saleId = (int)item.sale_id }, transaction);

                // 5. Audit Log
                string medName = await connection.ExecuteScalarAsync<string>("SELECT name FROM medicines WHERE id = @id", new { id = (int)item.medicine_id }, transaction) ?? "Unknown Medicine";

                await transaction.CommitAsync();

                _eventBus.Publish(InventoryChangeType.StockAdjusted);
                await _auditRepo.InsertLogAsync(currentUserId, "Item Returned", 
                    $"Returned {returnQty}x {medName} from Sale ID: {item.sale_id}. Stock restored. Total adjusted by -{deduction:N2}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"Failed to process return for sale item {saleItemId}", ex);
                throw;
            }
        }

        public async Task<FinancialReport> GetFinancialReportAsync(DateTime date)
        {
            const string query = @"
                SELECT 
                    COUNT(*) as TotalSalesCount,
                    COALESCE(SUM(grand_total), 0) as GrossSales,
                    COALESCE(SUM(tax_amount), 0) as TotalTax,
                    COALESCE(SUM(discount_amount), 0) as TotalDiscount,
                    COUNT(*) FILTER (WHERE fbr_reported = true) as FbrSalesCount,
                    COUNT(*) FILTER (WHERE fbr_reported = false) as InternalSalesCount
                FROM sales 
                WHERE sale_date::date = @date AND status != 'Voided'";

            const string returnsQuery = @"
                SELECT 
                    COUNT(*) as ReturnsCount,
                    COALESCE(SUM(returned_qty * unit_price), 0) as TotalReturns
                FROM sale_items si
                JOIN sales s ON si.sale_id = s.id
                WHERE s.sale_date::date = @date AND si.returned_qty > 0";

            using var conn = _db.GetConnection();
            var report = await conn.QuerySingleAsync<FinancialReport>(query, new { date = date.Date });
            var returnData = await conn.QuerySingleAsync(returnsQuery, new { date = date.Date });

            report.ReportDate = date;
            report.ReturnsCount = (int)returnData.returnscount;
            report.TotalReturns = (decimal)returnData.totalreturns;
            report.NetSales = report.GrossSales - report.TotalReturns;

            return report;
        }
    }

    public class SaleSummary
    {
        public string BillNo { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool FbrReported { get; set; }
        public string FbrStatus => FbrReported ? "Sent" : "Not Sent";
    }
}
