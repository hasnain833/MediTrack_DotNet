using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MySql.Data.MySqlClient;

namespace MediTrack.Repositories
{
    public class SaleRepository
    {
        private readonly DatabaseService _db;

        public SaleRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task CreateTransactionAsync(string billNo, int userId, int? customerId, List<SaleItem> items, decimal total, decimal tax, decimal discount, decimal grandTotal)
        {
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Insert Sale record
                const string saleQuery = @"
                    INSERT INTO sales (bill_no, customer_id, user_id, total_amount, tax_amount, discount_amount, grand_total, sale_date)
                    VALUES (@billNo, @customerId, @userId, @total, @tax, @discount, @grandTotal, @saleDate);
                    SELECT LAST_INSERT_ID();";

                using var saleCmd = new MySqlCommand(saleQuery, connection, (MySqlTransaction)transaction);
                saleCmd.Parameters.AddWithValue("@billNo", billNo);
                saleCmd.Parameters.AddWithValue("@customerId", customerId ?? (object)DBNull.Value);
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
                        INSERT INTO sale_items (sale_id, inventory_id, quantity, unit_price, subtotal)
                        VALUES (@saleId, @invId, @qty, @price, @subtotal)";
                    
                    using var itemCmd = new MySqlCommand(itemQuery, connection, (MySqlTransaction)transaction);
                    itemCmd.Parameters.AddWithValue("@saleId", saleId);
                    itemCmd.Parameters.AddWithValue("@invId", item.InventoryId);
                    itemCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    itemCmd.Parameters.AddWithValue("@price", item.UnitPrice);
                    itemCmd.Parameters.AddWithValue("@subtotal", item.Subtotal);
                    await itemCmd.ExecuteNonQueryAsync();

                    const string stockQuery = "UPDATE inventory SET stock_qty = stock_qty - @qty WHERE id = @invId";
                    using var stockCmd = new MySqlCommand(stockQuery, connection, (MySqlTransaction)transaction);
                    stockCmd.Parameters.AddWithValue("@qty", item.Quantity);
                    stockCmd.Parameters.AddWithValue("@invId", item.InventoryId);
                    await stockCmd.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
