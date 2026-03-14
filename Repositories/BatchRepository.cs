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
                const string query = "SELECT * FROM inventory_batches WHERE medicine_id = @medId AND remaining_units > 0 ORDER BY expiry_date ASC";
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
                Id                 = Convert.ToInt32(reader["id"]),
                MedicineId         = Convert.ToInt32(reader["medicine_id"]),
                SupplierId         = Convert.ToInt32(reader["supplier_id"]),
                BatchNo            = reader["batch_no"].ToString() ?? string.Empty,
                QuantityUnits      = Convert.ToInt32(reader["quantity_units"]),
                PurchaseTotalPrice = Convert.ToDecimal(reader["purchase_total_price"]),
                UnitCost           = Convert.ToDecimal(reader["unit_cost"]),
                SellingPrice       = Convert.ToDecimal(reader["selling_price"]),
                RemainingUnits     = Convert.ToInt32(reader["remaining_units"]),
                ManufactureDate    = reader["manufacture_date"] != DBNull.Value ? Convert.ToDateTime(reader["manufacture_date"]) : null,
                ExpiryDate         = Convert.ToDateTime(reader["expiry_date"]),
                InvoiceNo          = reader["invoice_no"].ToString() ?? string.Empty,
                InvoiceDate        = reader["invoice_date"] != DBNull.Value ? Convert.ToDateTime(reader["invoice_date"]) : null,
                CreatedAt          = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(InventoryBatch batch)
        {
            _auth.EnforceAdmin();
            try
            {
                const string query = @"
                    INSERT INTO inventory_batches (medicine_id, supplier_id, batch_no, quantity_units, purchase_total_price, unit_cost, selling_price, remaining_units, manufacture_date, expiry_date, invoice_no, invoice_date)
                    VALUES (@medId, @supId, @batch, @qtyU, @pTotal, @uCost, @sPrice, @remU, @mDate, @eDate, @invNo, @invDate)";
                
                var parameters = new Dictionary<string, object>
                {
                    { "@medId", batch.MedicineId },
                    { "@supId", batch.SupplierId },
                    { "@batch", batch.BatchNo },
                    { "@qtyU", batch.QuantityUnits },
                    { "@pTotal", batch.PurchaseTotalPrice },
                    { "@uCost", batch.UnitCost },
                    { "@sPrice", batch.SellingPrice },
                    { "@remU", batch.RemainingUnits },
                    { "@mDate", batch.ManufactureDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value },
                    { "@eDate", batch.ExpiryDate.ToString("yyyy-MM-dd") },
                    { "@invNo", batch.InvoiceNo },
                    { "@invDate", batch.InvoiceDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value }
                };

                await _db.ExecuteNonQueryAsync(query, parameters);
                AppLogger.LogInfo($"Batch added: medicine_id={batch.MedicineId}, batch={batch.BatchNo}");
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
                        (medicine_id, supplier_id, batch_no, quantity_units, purchase_total_price, unit_cost, selling_price, remaining_units, expiry_date, invoice_no, invoice_date)
                    VALUES
                        (@medId, @supId, @batch, @qtyU, @pTotal, @uCost, @sPrice, @remU, @eDate, @invNo, @invDate)";

                int count = 0;
                foreach (var batch in batches)
                {
                    using var cmd = new NpgsqlCommand(query, connection, transaction);
                    cmd.Parameters.AddWithValue("@medId",  batch.MedicineId);
                    cmd.Parameters.AddWithValue("@supId",  batch.SupplierId > 0 ? (object)batch.SupplierId : DBNull.Value);
                    cmd.Parameters.AddWithValue("@batch",  batch.BatchNo);
                    cmd.Parameters.AddWithValue("@qtyU",   batch.QuantityUnits);
                    cmd.Parameters.AddWithValue("@pTotal", batch.PurchaseTotalPrice);
                    cmd.Parameters.AddWithValue("@uCost",  batch.UnitCost);
                    cmd.Parameters.AddWithValue("@sPrice", batch.SellingPrice);
                    cmd.Parameters.AddWithValue("@remU",   batch.RemainingUnits);
                    cmd.Parameters.AddWithValue("@eDate",  batch.ExpiryDate.Date);
                    cmd.Parameters.AddWithValue("@invNo",  batch.InvoiceNo ?? string.Empty);
                    cmd.Parameters.AddWithValue("@invDate", batch.InvoiceDate.HasValue ? (object)batch.InvoiceDate.Value.Date : DBNull.Value);
                    await cmd.ExecuteNonQueryAsync();
                    count++;
                    AppLogger.LogInfo($"[StockIn] Inserted: medicine_id={batch.MedicineId}, batch={batch.BatchNo}, remaining={batch.RemainingUnits}");
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
