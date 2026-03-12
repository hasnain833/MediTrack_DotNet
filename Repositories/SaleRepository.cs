using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Services;
using DChemist.Utils;
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

        public async Task CreateTransactionAsync(string billNo, int userId, int? customerId, List<SaleItem> items,
            decimal total, decimal tax, decimal discount, decimal grandTotal, string? fbrInvoiceNo = null, string? fbrResponse = null)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var item in items)
                {
                    int baseQty = item.BaseQtyDeducted > 0 ? item.BaseQtyDeducted : item.Quantity;

                    const string totalStockQuery = @"
                        SELECT COALESCE(SUM(stock_qty), 0)
                        FROM inventory_batches
                        WHERE medicine_id = @medId AND stock_qty > 0";
                    if (!item.MedicineId.HasValue) continue;

                    using var stockCmd = new NpgsqlCommand(totalStockQuery, connection, transaction);
                    stockCmd.Parameters.AddWithValue("@medId", item.MedicineId.Value);
                    var totalAvailable = Convert.ToInt32(await stockCmd.ExecuteScalarAsync() ?? 0);

                    if (totalAvailable < baseQty)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{item.MedicineName}'. " +
                            $"Requested: {baseQty} {(item.SoldUnit ?? "unit")}(s) in base units. Available: {totalAvailable}.");
                    }
                }

                // ── Step 2: Insert Sale Record ────────────────────────────────────
                const string saleQuery = @"
                    INSERT INTO sales (bill_no, customer_id, user_id, total_amount, tax_amount, discount_amount, grand_total, fbr_invoice_no, fbr_response, sale_date)
                    VALUES (@billNo, @customerId, @userId, @total, @tax, @discount, @grandTotal, @fbrNo, @fbrResp, @saleDate)
                    RETURNING id;";

                using var saleCmd = new NpgsqlCommand(saleQuery, connection, transaction);
                saleCmd.Parameters.AddWithValue("@billNo", billNo);
                saleCmd.Parameters.AddWithValue("@customerId", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
                saleCmd.Parameters.AddWithValue("@userId", userId);
                saleCmd.Parameters.AddWithValue("@total", total);
                saleCmd.Parameters.AddWithValue("@tax", tax);
                saleCmd.Parameters.AddWithValue("@discount", discount);
                saleCmd.Parameters.AddWithValue("@grandTotal", grandTotal);
                saleCmd.Parameters.AddWithValue("@fbrNo", (object?)fbrInvoiceNo ?? DBNull.Value);
                saleCmd.Parameters.AddWithValue("@fbrResp", (object?)fbrResponse ?? DBNull.Value);
                saleCmd.Parameters.AddWithValue("@saleDate", DateTime.Now);

                int saleId = Convert.ToInt32(await saleCmd.ExecuteScalarAsync());

                // ── Step 3: Insert Sale Items & Deduct Stock (FIFO) ──────────────
                foreach (var item in items)
                {
                    // Use BaseQtyDeducted to know how many base units to pull from inventory.
                    // If not set (legacy), fall back to Quantity.
                    int baseQty = item.BaseQtyDeducted > 0 ? item.BaseQtyDeducted : item.Quantity;

                    const string itemQuery = @"
                        INSERT INTO sale_items (sale_id, medicine_id, batch_id, quantity, unit_price, subtotal, sold_unit, base_qty_deducted)
                        VALUES (@saleId, @medId, @batchId, @qty, @price, @subtotal, @soldUnit, @baseQty)";

                    using var itemCmd = new NpgsqlCommand(itemQuery, connection, transaction);
                    itemCmd.Parameters.AddWithValue("@saleId", saleId);
                    itemCmd.Parameters.AddWithValue("@medId", (object?)item.MedicineId ?? DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@batchId", (object?)item.BatchId ?? DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    itemCmd.Parameters.AddWithValue("@price", item.UnitPrice);
                    itemCmd.Parameters.AddWithValue("@subtotal", item.Subtotal);
                    itemCmd.Parameters.AddWithValue("@soldUnit", item.SoldUnit ?? (object)DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@baseQty", baseQty);
                    await itemCmd.ExecuteNonQueryAsync();

                    if (item.MedicineId.HasValue)
                    {
                        // FIFO Logic: Fetch all batches ordered by earliest expiry, deduct in base units
                        const string getBatchesQuery = @"
                            SELECT id, stock_qty 
                            FROM inventory_batches 
                            WHERE medicine_id = @medId AND stock_qty > 0 
                            ORDER BY expiry_date ASC, created_at ASC 
                            FOR UPDATE"; // Lock rows for consistency
                        
                        using var getBatchesCmd = new NpgsqlCommand(getBatchesQuery, connection, transaction);
                        getBatchesCmd.Parameters.AddWithValue("@medId", item.MedicineId.Value);
                        
                        int remainingToDeduct = baseQty;
                        using var reader = await getBatchesCmd.ExecuteReaderAsync();
                        var batchesToUpdate = new List<(int Id, int Deduct)>();
                        
                        while (remainingToDeduct > 0 && await reader.ReadAsync())
                        {
                            int batchId = reader.GetInt32(0);
                            int availableInBatch = reader.GetInt32(1);
                            int deductFromBatch = Math.Min(remainingToDeduct, availableInBatch);
                            
                            batchesToUpdate.Add((batchId, deductFromBatch));
                            remainingToDeduct -= deductFromBatch;
                        }
                        reader.Close();

                        if (remainingToDeduct > 0)
                        {
                            throw new InvalidOperationException($"Insufficient total stock for '{item.MedicineName}' during final processing.");
                        }

                        foreach (var batch in batchesToUpdate)
                        {
                            const string updateQuery = "UPDATE inventory_batches SET stock_qty = stock_qty - @qty WHERE id = @id";
                            using var updateCmd = new NpgsqlCommand(updateQuery, connection, transaction);
                            updateCmd.Parameters.AddWithValue("@qty", batch.Deduct);
                            updateCmd.Parameters.AddWithValue("@id", batch.Id);
                            await updateCmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                await transaction.CommitAsync();

                // ── Step 4: Notify all subscribed screens (post-commit) ───────────
                _eventBus.Publish(InventoryChangeType.StockDeducted);
                
                // ── Step 5: Log Audit ─────────────────────────────────────────────
                _ = _auditRepo.InsertLogAsync(userId, "Sale Created", $"Bill No: {billNo}, Total: {grandTotal:F2}");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("SaleRepository.CreateTransactionAsync failed", ex);
                throw;
            }
        }

        public async Task<List<SaleSummary>> GetAllSummariesAsync(int limit = 50)
        {
            const string query = @"
                SELECT 
                    s.bill_no, 
                    COALESCE(c.customer_name, 'Walking Customer') as customer, 
                    s.grand_total as amount, 
                    to_char(s.sale_date, 'DD Mon YYYY HH24:MI') as date,
                    s.status
                FROM sales s
                LEFT JOIN customers c ON s.customer_id = c.id
                ORDER BY s.sale_date DESC
                LIMIT @limit";
            
            var parameters = new Dictionary<string, object> { { "@limit", limit } };
            return await _db.FetchAllAsync(query, MapSaleSummary, parameters);
        }

        public async Task<decimal> GetRevenueTotalAsync(DateTime start, DateTime end)
        {
            // Only count completed sales
            const string query = "SELECT COALESCE(SUM(grand_total), 0) FROM sales WHERE sale_date >= @start AND sale_date <= @end AND status = 'Completed'";
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
        }

        public async Task<Sale?> GetSaleWithItemsAsync(string billNo)
        {
            const string saleQuery = "SELECT * FROM sales WHERE bill_no = @billNo";
            var parameters = new Dictionary<string, object> { { "@billNo", billNo } };
            
            var sale = await _db.FetchOneAsync(saleQuery, reader => new Sale
            {
                Id = Convert.ToInt32(reader["id"]),
                BillNo = reader["bill_no"].ToString() ?? "",
                UserId = Convert.ToInt32(reader["user_id"]),
                CustomerId = reader["customer_id"] != DBNull.Value ? Convert.ToInt32(reader["customer_id"]) : null,
                TotalAmount = Convert.ToDecimal(reader["total_amount"]),
                TaxAmount = Convert.ToDecimal(reader["tax_amount"]),
                DiscountAmount = Convert.ToDecimal(reader["discount_amount"]),
                GrandTotal = Convert.ToDecimal(reader["grand_total"]),
                SaleDate = Convert.ToDateTime(reader["sale_date"]),
                Status = reader["status"].ToString() ?? "Completed"
            }, parameters);

            if (sale != null)
            {
                const string itemsQuery = @"
                    SELECT si.*, m.name as medicine_name 
                    FROM sale_items si 
                    LEFT JOIN medicines m ON si.medicine_id = m.id 
                    WHERE si.sale_id = @saleId";
                var itemsParams = new Dictionary<string, object> { { "@saleId", sale.Id } };
                
                sale.Items = await _db.FetchAllAsync(itemsQuery, reader => new SaleItem
                {
                    Id = Convert.ToInt32(reader["id"]),
                    SaleId = Convert.ToInt32(reader["sale_id"]),
                    MedicineId = reader["medicine_id"] != DBNull.Value ? Convert.ToInt32(reader["medicine_id"]) : null,
                    BatchId = reader["batch_id"] != DBNull.Value ? Convert.ToInt32(reader["batch_id"]) : null,
                    MedicineName = reader["medicine_name"].ToString() ?? "Unknown",
                    Quantity = Convert.ToInt32(reader["quantity"]),
                    ReturnedQuantity = Convert.ToInt32(reader["returned_qty"]),
                    UnitPrice = Convert.ToDecimal(reader["unit_price"]),
                    Subtotal = Convert.ToDecimal(reader["subtotal"]),
                    SoldUnit = reader["sold_unit"] != DBNull.Value ? reader["sold_unit"].ToString() : null,
                    BaseQtyDeducted = reader["base_qty_deducted"] != DBNull.Value ? Convert.ToInt32(reader["base_qty_deducted"]) : 0
                }, itemsParams);
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
                const string updateSaleQuery = "UPDATE sales SET status = 'Voided' WHERE id = @id";
                using var updateSaleCmd = new NpgsqlCommand(updateSaleQuery, connection, transaction);
                updateSaleCmd.Parameters.AddWithValue("@id", sale.Id);
                await updateSaleCmd.ExecuteNonQueryAsync();

                // 2. Restore Inventory Stock
                foreach (var item in sale.Items)
                {
                    if (item.BatchId.HasValue && item.MedicineId.HasValue)
                    {
                        // Directly restore to the specific batch that was deducted if batch_id was stored properly
                        const string restoreQuery = "UPDATE inventory_batches SET stock_qty = stock_qty + @qty WHERE id = @batchId";
                        using var restoreCmd = new NpgsqlCommand(restoreQuery, connection, transaction);
                        restoreCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        restoreCmd.Parameters.AddWithValue("@batchId", item.BatchId.Value);
                        await restoreCmd.ExecuteNonQueryAsync();
                    }
                    else if (item.MedicineId.HasValue)
                    {
                        // Fallback: Restore to the latest batch
                        const string restoreFallbackQuery = @"
                            UPDATE inventory_batches 
                            SET stock_qty = stock_qty + @qty 
                            WHERE id = (
                                SELECT id FROM inventory_batches 
                                WHERE medicine_id = @medId 
                                ORDER BY expiry_date DESC LIMIT 1
                            )";
                        using var restoreFallbackCmd = new NpgsqlCommand(restoreFallbackQuery, connection, transaction);
                        restoreFallbackCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        restoreFallbackCmd.Parameters.AddWithValue("@medId", item.MedicineId.Value);
                        await restoreFallbackCmd.ExecuteNonQueryAsync();
                    }
                }

                await transaction.CommitAsync();

                _eventBus.Publish(InventoryChangeType.StockAdjusted);
                _ = _auditRepo.InsertLogAsync(currentUserId, "Sale Voided", $"Bill No: {billNo} voided. Stock restored.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"Failed to void sale {billNo}", ex);
                throw;
            }
        }

        public async Task ProcessReturnAsync(int saleId, int medicineId, int batchId, int returnQty, int currentUserId)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Update returned_qty in sale_items
                const string updateItemQuery = @"
                    UPDATE sale_items 
                    SET returned_qty = returned_qty + @qty 
                    WHERE sale_id = @saleId AND medicine_id = @medId AND batch_id = @batchId
                    RETURNING (quantity - returned_qty) as remaining";
                
                using var updateCmd = new NpgsqlCommand(updateItemQuery, connection, transaction);
                updateCmd.Parameters.AddWithValue("@qty", returnQty);
                updateCmd.Parameters.AddWithValue("@saleId", saleId);
                updateCmd.Parameters.AddWithValue("@medId", medicineId);
                updateCmd.Parameters.AddWithValue("@batchId", batchId);

                var remainingObj = await updateCmd.ExecuteScalarAsync();
                if (remainingObj == null) throw new Exception("Sale item not found.");
                int remaining = Convert.ToInt32(remainingObj);
                
                if (remaining < 0) throw new InvalidOperationException("Return quantity exceeds original purchased quantity.");

                // 2. Restore stock to inventory
                const string restoreStockQuery = "UPDATE inventory_batches SET stock_qty = stock_qty + @qty WHERE id = @batchId";
                using var restoreCmd = new NpgsqlCommand(restoreStockQuery, connection, transaction);
                restoreCmd.Parameters.AddWithValue("@qty", returnQty);
                restoreCmd.Parameters.AddWithValue("@batchId", batchId);
                await restoreCmd.ExecuteNonQueryAsync();

                // 3. Log Audit
                const string getMedNameQuery = "SELECT name FROM medicines WHERE id = @id";
                using var nameCmd = new NpgsqlCommand(getMedNameQuery, connection, transaction);
                nameCmd.Parameters.AddWithValue("@id", medicineId);
                string medName = (await nameCmd.ExecuteScalarAsync())?.ToString() ?? "Unknown Medicine";

                await transaction.CommitAsync();

                _eventBus.Publish(InventoryChangeType.StockAdjusted);
                _ = _auditRepo.InsertLogAsync(currentUserId, "Item Returned", 
                    $"Returned {returnQty}x {medName} from Sale ID: {saleId}. Stock restored.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"Failed to process return for sale {saleId}", ex);
                throw;
            }
        }

        private static SaleSummary MapSaleSummary(NpgsqlDataReader reader)
        {
            return new SaleSummary
            {
                BillNo = reader["bill_no"].ToString() ?? "",
                Customer = reader["customer"].ToString() ?? "",
                Amount = Convert.ToDecimal(reader["amount"]),
                Date = reader["date"].ToString() ?? "",
                Status = reader["status"].ToString() ?? ""
            };
        }
    }

    public class SaleSummary
    {
        public string BillNo { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Date { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
