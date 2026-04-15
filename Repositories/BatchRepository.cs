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
                const string query = "SELECT * FROM inventory_batches WHERE medicine_id = @medicineId AND remaining_units > 0 ORDER BY expiry_date ASC";
                using var conn = _db.GetConnection();
                var batches = await conn.QueryAsync<InventoryBatch>(query, new { medicineId });
                return batches.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"BatchRepository.GetByMedicineIdAsync failed for medicine_id={medicineId}", ex);
                throw new DataAccessException("Could not load batch information.", ex);
            }
        }

        public async Task<int> GetTotalStockAsync(int medicineId)
        {
            try
            {
                const string query = "SELECT COALESCE(SUM(remaining_units), 0) FROM inventory_batches WHERE medicine_id = @medicineId AND remaining_units > 0";
                using var conn = _db.GetConnection();
                return await conn.ExecuteScalarAsync<int>(query, new { medicineId });
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"BatchRepository.GetTotalStockAsync failed for medicine_id={medicineId}", ex);
                return 0;
            }
        }

        public async Task<List<AlertBatchItem>> GetLowStockAsync(int threshold = 10)
        {
            try
            {
                const string query = @"
                    SELECT m.id AS MedicineId, m.name AS MedicineName, SUM(b.remaining_units) AS TotalUnits
                    FROM inventory_batches b
                    JOIN medicines m ON m.id = b.medicine_id
                    WHERE b.remaining_units > 0
                    GROUP BY m.id, m.name
                    HAVING SUM(b.remaining_units) <= @threshold
                    ORDER BY SUM(b.remaining_units) ASC
                    LIMIT 50";
                using var conn = _db.GetConnection();
                var rows = await conn.QueryAsync<AlertBatchItem>(query, new { threshold });
                return rows.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BatchRepository.GetLowStockAsync failed", ex);
                return new List<AlertBatchItem>();
            }
        }

        public async Task<List<AlertBatchItem>> GetNearExpiryAsync(int daysAhead = 90)
        {
            try
            {
                const string query = @"
                    SELECT b.id AS BatchId, b.medicine_id AS MedicineId, m.name AS MedicineName, b.expiry_date AS ExpiryDate, b.remaining_units AS TotalUnits
                    FROM inventory_batches b
                    JOIN medicines m ON m.id = b.medicine_id
                    WHERE b.remaining_units > 0
                      AND b.expiry_date <= CURRENT_DATE + @daysAhead
                    ORDER BY b.expiry_date ASC
                    LIMIT 50";
                using var conn = _db.GetConnection();
                var rows = await conn.QueryAsync<AlertBatchItem>(query, new { daysAhead });
                return rows.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BatchRepository.GetNearExpiryAsync failed", ex);
                return new List<AlertBatchItem>();
            }
        }

        public async Task AddAsync(InventoryBatch batch)
        {
            _auth.EnforceAdmin();
            try
            {
                const string query = @"
                    INSERT INTO inventory_batches (
                        medicine_id, supplier_id, batch_no, quantity_units, 
                        purchase_total_price, unit_cost, selling_price, 
                        remaining_units, manufacture_date, expiry_date, 
                        invoice_no, invoice_date, entry_mode, units_per_pack, pack_quantity
                    )
                    VALUES (
                        @MedicineId, @SupplierId, @BatchNo, @QuantityUnits, 
                        @PurchaseTotalPrice, @UnitCost, @SellingPrice, 
                        @RemainingUnits, @ManufactureDate, @ExpiryDate, 
                        @InvoiceNo, @InvoiceDate, @EntryMode, @UnitsPerPack, @PackQuantity
                    )";
                
                using var conn = _db.GetConnection();
                await conn.ExecuteAsync(query, batch);
                AppLogger.LogInfo($"Batch added: medicine_id={batch.MedicineId}, batch={batch.BatchNo}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BatchRepository.AddAsync failed", ex);
                throw new DataAccessException("Could not add the inventory batch.", ex);
            }
        }

        public async Task AddBulkAsync(IEnumerable<InventoryBatch> batches)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            try
            {
                const string query = @"
                    INSERT INTO inventory_batches (
                        medicine_id, supplier_id, batch_no, quantity_units, 
                        purchase_total_price, unit_cost, selling_price, 
                        remaining_units, expiry_date, invoice_no, invoice_date,
                        entry_mode, units_per_pack, pack_quantity
                    )
                    VALUES (
                        @MedicineId, @SupplierId, @BatchNo, @QuantityUnits, 
                        @PurchaseTotalPrice, @UnitCost, @SellingPrice, 
                        @RemainingUnits, @ExpiryDate, @InvoiceNo, @InvoiceDate,
                        @EntryMode, @UnitsPerPack, @PackQuantity
                    )";

                await connection.ExecuteAsync(query, batches, transaction);

                await transaction.CommitAsync();
                AppLogger.LogInfo($"[StockIn] Bulk save committed — {batches.Count()} batch(es).");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("BatchRepository.AddBulkAsync failed — rolled back", ex);
                throw new DataAccessException("Bulk stock save failed. All changes have been rolled back.", ex);
            }
        }
        public async Task UpdateStockManualAsync(int batchId, int newQty, string reason, int userId, AuditRepository auditRepo)
        {
            _auth.EnforceAdmin(); // Only admins can do manual stock adjustments
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Get current stock for logging difference
                const string getQuery = "SELECT remaining_units, batch_no FROM inventory_batches WHERE id = @batchId";
                var current = await connection.QuerySingleOrDefaultAsync(getQuery, new { batchId }, transaction);
                if (current == null) throw new InvalidOperationException("Batch not found.");

                int oldQty = current.remaining_units;
                string batchNo = current.batch_no;

                // 2. Update stock
                const string updateQuery = "UPDATE inventory_batches SET remaining_units = @newQty WHERE id = @batchId";
                await connection.ExecuteAsync(updateQuery, new { newQty, batchId }, transaction);

                // 3. Log to audit
                await auditRepo.InsertLogAsync(userId, "Stock Adjustment", 
                    $"Manual adjustment for Batch {batchNo}: {oldQty} -> {newQty}. Reason: {reason}", 
                    connection, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"BatchRepository.UpdateStockManualAsync failed for batch {batchId}", ex);
                throw new DataAccessException("Stock adjustment failed. Please try again.", ex);
            }
        }
    }
}
