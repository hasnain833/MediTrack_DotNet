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

        public SaleRepository(DatabaseService db, AuthorizationService auth, InventoryEventBus eventBus)
        {
            _db = db;
            _auth = auth;
            _eventBus = eventBus;
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
                // ── Step 1: Pre-flight stock validation ──────────────────────────
                // Check ALL items BEFORE touching any data. If any item has
                // insufficient stock, we abort cleanly with a user-friendly error.
                foreach (var item in items)
                {
                    if (!item.BatchId.HasValue) continue;

                    const string checkQuery = "SELECT stock_qty FROM inventory_batches WHERE id = @batchId";
                    using var checkCmd = new NpgsqlCommand(checkQuery, connection, transaction);
                    checkCmd.Parameters.AddWithValue("@batchId", item.BatchId.Value);
                    var available = await checkCmd.ExecuteScalarAsync();
                    var availableQty = available == DBNull.Value ? 0 : Convert.ToInt32(available);

                    if (availableQty < item.Quantity)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for '{item.MedicineName}'. " +
                            $"Requested: {item.Quantity}, Available: {availableQty}.");
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
                    const string itemQuery = @"
                        INSERT INTO sale_items (sale_id, medicine_id, batch_id, quantity, unit_price, subtotal)
                        VALUES (@saleId, @medId, @batchId, @qty, @price, @subtotal)";

                    using var itemCmd = new NpgsqlCommand(itemQuery, connection, transaction);
                    itemCmd.Parameters.AddWithValue("@saleId", saleId);
                    itemCmd.Parameters.AddWithValue("@medId", (object?)item.MedicineId ?? DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@batchId", (object?)item.BatchId ?? DBNull.Value);
                    itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    itemCmd.Parameters.AddWithValue("@price", item.UnitPrice);
                    itemCmd.Parameters.AddWithValue("@subtotal", item.Subtotal);
                    await itemCmd.ExecuteNonQueryAsync();

                    if (item.MedicineId.HasValue)
                    {
                        // FIFO Logic: Fetch all batches for this medicine ordered by expiry
                        const string getBatchesQuery = @"
                            SELECT id, stock_qty 
                            FROM inventory_batches 
                            WHERE medicine_id = @medId AND stock_qty > 0 
                            ORDER BY expiry_date ASC, created_at ASC 
                            FOR UPDATE"; // Lock rows for consistency
                        
                        using var getBatchesCmd = new NpgsqlCommand(getBatchesQuery, connection, transaction);
                        getBatchesCmd.Parameters.AddWithValue("@medId", item.MedicineId.Value);
                        
                        int remainingToDeduct = item.Quantity;
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
                    'Paid' as status
                FROM sales s
                LEFT JOIN customers c ON s.customer_id = c.id
                ORDER BY s.sale_date DESC
                LIMIT @limit";
            
            var parameters = new Dictionary<string, object> { { "@limit", limit } };
            return await _db.FetchAllAsync(query, MapSaleSummary, parameters);
        }

        public async Task<decimal> GetRevenueTotalAsync(DateTime start, DateTime end)
        {
            const string query = "SELECT COALESCE(SUM(grand_total), 0) FROM sales WHERE sale_date >= @start AND sale_date <= @end";
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("@start", start);
            cmd.Parameters.AddWithValue("@end", end);
            return Convert.ToDecimal(await cmd.ExecuteScalarAsync());
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
