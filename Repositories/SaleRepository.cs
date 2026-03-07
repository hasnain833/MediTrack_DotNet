using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Npgsql;

namespace MediTrack.Repositories
{
    public class SaleRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public SaleRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task CreateTransactionAsync(string billNo, int userId, int? customerId, List<SaleItem> items,
            decimal total, decimal tax, decimal discount, decimal grandTotal)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // 1. Insert Sale record and return new ID
                const string saleQuery = @"
                    INSERT INTO sales (bill_no, customer_id, user_id, total_amount, tax_amount, discount_amount, grand_total, sale_date)
                    VALUES (@billNo, @customerId, @userId, @total, @tax, @discount, @grandTotal, @saleDate)
                    RETURNING id;";

                using var saleCmd = new NpgsqlCommand(saleQuery, connection, transaction);
                saleCmd.Parameters.AddWithValue("@billNo", billNo);
                saleCmd.Parameters.AddWithValue("@customerId", customerId.HasValue ? (object)customerId.Value : DBNull.Value);
                saleCmd.Parameters.AddWithValue("@userId", userId);
                saleCmd.Parameters.AddWithValue("@total", total);
                saleCmd.Parameters.AddWithValue("@tax", tax);
                saleCmd.Parameters.AddWithValue("@discount", discount);
                saleCmd.Parameters.AddWithValue("@grandTotal", grandTotal);
                saleCmd.Parameters.AddWithValue("@saleDate", DateTime.Now);

                int saleId = Convert.ToInt32(await saleCmd.ExecuteScalarAsync());

                // 2. Insert Sale Items and Update Stock
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

                    if (item.BatchId.HasValue)
                    {
                        const string stockQuery = "UPDATE inventory_batches SET stock_qty = stock_qty - @qty WHERE id = @batchId";
                        using var stockCmd = new NpgsqlCommand(stockQuery, connection, transaction);
                        stockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                        stockCmd.Parameters.AddWithValue("@batchId", item.BatchId.Value);
                        await stockCmd.ExecuteNonQueryAsync();
                    }
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
