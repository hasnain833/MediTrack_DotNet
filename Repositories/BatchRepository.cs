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
    public class BatchRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public BatchRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<InventoryBatch>> GetByMedicineIdAsync(int medicineId)
        {
            try
            {
                const string query = "SELECT * FROM inventory_batches WHERE medicine_id = @medId AND stock_qty > 0 ORDER BY expiry_date ASC";
                var parameters = new Dictionary<string, object> { { "@medId", medicineId } };
                return await _db.FetchAllAsync(query, MapBatch, parameters);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"BatchRepository.GetByMedicineIdAsync failed for medicine_id={medicineId}", ex);
                throw new DataAccessException("Could not load batch information.", ex);
            }
        }

        private static InventoryBatch MapBatch(NpgsqlDataReader reader)
        {
            return new InventoryBatch
            {
                Id              = Convert.ToInt32(reader["id"]),
                MedicineId      = Convert.ToInt32(reader["medicine_id"]),
                SupplierId      = Convert.ToInt32(reader["supplier_id"]),
                BatchNumber     = reader["batch_number"].ToString() ?? string.Empty,
                PurchasePrice   = Convert.ToDecimal(reader["purchase_price"]),
                SellingPrice    = Convert.ToDecimal(reader["selling_price"]),
                StockQty        = Convert.ToInt32(reader["stock_qty"]),
                ManufactureDate = reader["manufacture_date"] != DBNull.Value ? Convert.ToDateTime(reader["manufacture_date"]) : null,
                ExpiryDate      = Convert.ToDateTime(reader["expiry_date"]),
                CreatedAt       = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(InventoryBatch batch)
        {
            _auth.EnforceAdmin();
            try
            {
                const string query = @"
                    INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, manufacture_date, expiry_date)
                    VALUES (@medId, @supId, @batch, @pPrice, @sPrice, @qty, @mDate, @eDate)";
                
                var parameters = new Dictionary<string, object>
                {
                    { "@medId", batch.MedicineId },
                    { "@supId", batch.SupplierId },
                    { "@batch", batch.BatchNumber },
                    { "@pPrice", batch.PurchasePrice },
                    { "@sPrice", batch.SellingPrice },
                    { "@qty", batch.StockQty },
                    { "@mDate", batch.ManufactureDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value },
                    { "@eDate", batch.ExpiryDate.ToString("yyyy-MM-dd") }
                };

                await _db.ExecuteNonQueryAsync(query, parameters);
                AppLogger.LogInfo($"Batch added: medicine_id={batch.MedicineId}, batch={batch.BatchNumber}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BatchRepository.AddAsync failed", ex);
                throw new DataAccessException("Could not add the inventory batch.", ex);
            }
        }

        /// <summary>
        /// Saves all receiving items as new inventory_batches rows in a single
        /// database transaction. Rolls back completely on any failure.
        /// </summary>
        public async Task AddBulkAsync(IEnumerable<InventoryBatch> batches)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                const string query = @"
                    INSERT INTO inventory_batches
                        (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, expiry_date)
                    VALUES
                        (@medId, @supId, @batch, @pPrice, @sPrice, @qty, @eDate)";

                int count = 0;
                foreach (var batch in batches)
                {
                    using var cmd = new NpgsqlCommand(query, connection, transaction);
                    cmd.Parameters.AddWithValue("@medId",  batch.MedicineId);
                    cmd.Parameters.AddWithValue("@supId",  batch.SupplierId > 0 ? (object)batch.SupplierId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@batch",  batch.BatchNumber);
                    cmd.Parameters.AddWithValue("@pPrice", batch.PurchasePrice);
                    cmd.Parameters.AddWithValue("@sPrice", batch.SellingPrice);
                    cmd.Parameters.AddWithValue("@qty",    batch.StockQty);
                    cmd.Parameters.AddWithValue("@eDate",  batch.ExpiryDate.Date);
                    await cmd.ExecuteNonQueryAsync();
                    count++;
                    AppLogger.LogInfo($"[StockIn] Inserted: medicine_id={batch.MedicineId}, batch={batch.BatchNumber}, qty={batch.StockQty}");
                }

                await transaction.CommitAsync();
                AppLogger.LogInfo($"[StockIn] Bulk save committed — {count} batch(es).");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("BatchRepository.AddBulkAsync failed — rolled back", ex);
                throw new DataAccessException("Bulk stock save failed. All changes have been rolled back.", ex);
            }
        }
    }
}
