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
    public class MedicineRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;
        private readonly InventoryEventBus _eventBus;

        public MedicineRepository(DatabaseService db, AuthorizationService auth, InventoryEventBus eventBus)
        {
            _db = db;
            _auth = auth;
            _eventBus = eventBus;
        }

        public async Task<List<Medicine>> GetAllAsync()
        {
            const string query = @"
                SELECT 
                    m.*, 
                    c.name as CategoryName, 
                    man.name as ManufacturerName,
                    MAX(s.name) as SupplierName,
                    COALESCE(SUM(b.remaining_units), 0) as StockQty,
                    MAX(b.selling_price) as SellingPrice,
                    MIN(b.unit_cost) as PurchasePrice,
                    MIN(b.expiry_date) as ExpiryDate
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                LEFT JOIN suppliers s ON b.supplier_id = s.id
                GROUP BY m.id, c.name, man.name
                ORDER BY m.name ASC";
            try
            {
                using var conn = _db.GetConnection();
                var results = await conn.QueryAsync<Medicine>(query);
                return results.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("MedicineRepository.GetAllAsync failed", ex);
                throw new DataAccessException("Could not load medicines. Please check your database connection.", ex);
            }
        }

        public async Task<List<Medicine>> SearchAsync(string text)
        {
            const string query = @"
                SELECT 
                    m.*, 
                    c.name as CategoryName, 
                    man.name as ManufacturerName,
                    MAX(s.name) as SupplierName,
                    COALESCE(SUM(b.remaining_units), 0) as StockQty,
                    MAX(b.selling_price) as SellingPrice,
                    MIN(b.unit_cost) as PurchasePrice,
                    MIN(b.expiry_date) as ExpiryDate
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                LEFT JOIN suppliers s ON b.supplier_id = s.id
                WHERE m.name ILIKE @text 
                   OR m.generic_name ILIKE @text 
                   OR m.barcode = @exact
                   OR man.name ILIKE @text
                GROUP BY m.id, c.name, man.name
                ORDER BY m.name ASC
                LIMIT 20";
            
            try
            {
                using var conn = _db.GetConnection();
                var results = await conn.QueryAsync<Medicine>(query, new { text = $"%{text}%", exact = text });
                return results.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"MedicineRepository.SearchAsync failed for '{text}'", ex);
                throw new DataAccessException("Search failed. Please try again.", ex);
            }
        }

        public async Task<Medicine?> GetByBarcodeAsync(string barcode)
        {
            const string query = @"
                SELECT 
                    m.*, 
                    c.name as CategoryName, 
                    man.name as ManufacturerName,
                    COALESCE(SUM(b.remaining_units), 0) as StockQty,
                    MAX(b.selling_price) as SellingPrice,
                    MIN(b.unit_cost) as PurchasePrice,
                    MIN(b.expiry_date) as ExpiryDate
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE m.barcode = @barcode 
                GROUP BY m.id, c.name, man.name
                LIMIT 1";
            
            try
            {
                using var conn = _db.GetConnection();
                return await conn.QuerySingleOrDefaultAsync<Medicine>(query, new { barcode });
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"MedicineRepository.GetByBarcodeAsync failed for barcode '{barcode}'", ex);
                throw new DataAccessException("Could not look up medicine by barcode.", ex);
            }
        }

        public async Task<Medicine> AddAsync(Medicine medicine)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Resolve Category, Manufacturer, Supplier IDs
                if (medicine.CategoryId == null && !string.IsNullOrWhiteSpace(medicine.CategoryName))
                    medicine.CategoryId = await GetOrCreateCategoryAsync(medicine.CategoryName, connection, transaction);
                
                if (medicine.ManufacturerId == null && !string.IsNullOrWhiteSpace(medicine.ManufacturerName))
                    medicine.ManufacturerId = await GetOrCreateManufacturerAsync(medicine.ManufacturerName, connection, transaction);

                int? supplierId = null;
                if (!string.IsNullOrWhiteSpace(medicine.SupplierName))
                    supplierId = await GetOrCreateSupplierAsync(medicine.SupplierName, connection, transaction);

                // 2. Insert Medicine
                const string medQuery = @"
                    INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode)
                    VALUES (@Name, @GenericName, @CategoryId, @ManufacturerId, @DosageForm, @Strength, @Barcode)
                    RETURNING id;";

                int medId = await connection.ExecuteScalarAsync<int>(medQuery, medicine, transaction);
                medicine.Id = medId;

                // 3. Insert Initial Batch
                if (medicine.StockQty > 0)
                {
                    const string batchQuery = @"
                        INSERT INTO inventory_batches (medicine_id, supplier_id, batch_no, quantity_units, purchase_total_price, unit_cost, selling_price, remaining_units, expiry_date)
                        VALUES (@medId, @supId, @batchNo, @qty, @pTotal, @uCost, @sPrice, @qty, @expiry)";

                    await connection.ExecuteAsync(batchQuery, new 
                    { 
                        medId, 
                        supId = supplierId, 
                        batchNo = "BATCH-" + DateTime.Now.ToString("yyyyMMdd"),
                        qty = medicine.StockQty,
                        pTotal = medicine.PurchasePrice * medicine.StockQty,
                        uCost = medicine.PurchasePrice,
                        sPrice = medicine.SellingPrice,
                        expiry = medicine.ExpiryDate ?? DateTime.Now.AddYears(1)
                    }, transaction);
                }
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("MedicineRepository.AddAsync failed", ex);
                throw new DataAccessException("Could not add medicine. Please try again.", ex);
            }

            _eventBus.Publish(InventoryChangeType.MedicineAdded);
            return medicine;
        }

        private async Task<int> GetOrCreateCategoryAsync(NpgsqlConnection conn, string name, NpgsqlTransaction trans)
        {
            var id = await conn.ExecuteScalarAsync<int?>("SELECT id FROM categories WHERE LOWER(name) = LOWER(@name) LIMIT 1", new { name }, trans);
            if (id.HasValue) return id.Value;

            return await conn.ExecuteScalarAsync<int>("INSERT INTO categories (name) VALUES (@name) RETURNING id", new { name }, trans);
        }

        // Overload for when we don't have a transaction but the old code passed (string, conn, trans)
        private async Task<int> GetOrCreateCategoryAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans) 
            => await GetOrCreateCategoryAsync(conn, name, trans);

        private async Task<int> GetOrCreateManufacturerAsync(NpgsqlConnection conn, string name, NpgsqlTransaction trans)
        {
            var id = await conn.ExecuteScalarAsync<int?>("SELECT id FROM manufacturers WHERE LOWER(name) = LOWER(@name) LIMIT 1", new { name }, trans);
            if (id.HasValue) return id.Value;

            return await conn.ExecuteScalarAsync<int>("INSERT INTO manufacturers (name) VALUES (@name) RETURNING id", new { name }, trans);
        }

        private async Task<int> GetOrCreateManufacturerAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans)
            => await GetOrCreateManufacturerAsync(conn, name, trans);

        private async Task<int> GetOrCreateSupplierAsync(NpgsqlConnection conn, string name, NpgsqlTransaction trans)
        {
            var id = await conn.ExecuteScalarAsync<int?>("SELECT id FROM suppliers WHERE LOWER(name) = LOWER(@name) LIMIT 1", new { name }, trans);
            if (id.HasValue) return id.Value;

            return await conn.ExecuteScalarAsync<int>("INSERT INTO suppliers (name) VALUES (@name) RETURNING id", new { name }, trans);
        }

        private async Task<int> GetOrCreateSupplierAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans)
            => await GetOrCreateSupplierAsync(conn, name, trans);

        public async Task UpdateAsync(Medicine medicine)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Resolve Category, Manufacturer, Supplier IDs
                if (medicine.CategoryId == null && !string.IsNullOrWhiteSpace(medicine.CategoryName))
                    medicine.CategoryId = await GetOrCreateCategoryAsync(medicine.CategoryName, connection, transaction);
                
                if (medicine.ManufacturerId == null && !string.IsNullOrWhiteSpace(medicine.ManufacturerName))
                    medicine.ManufacturerId = await GetOrCreateManufacturerAsync(medicine.ManufacturerName, connection, transaction);

                int? supplierId = null;
                if (!string.IsNullOrWhiteSpace(medicine.SupplierName))
                    supplierId = await GetOrCreateSupplierAsync(medicine.SupplierName, connection, transaction);

                // 2. Update Medicine Metadata
                const string medQuery = @"
                    UPDATE medicines 
                    SET name = @Name, generic_name = @GenericName, category_id = @CategoryId, 
                        manufacturer_id = @ManufacturerId, dosage_form = @DosageForm, strength = @Strength, barcode = @Barcode
                    WHERE id = @Id";
                
                await connection.ExecuteAsync(medQuery, medicine, transaction);

                // 3. Update Inventory Batches
                const string globalPriceUpdateQuery = @"
                    UPDATE inventory_batches 
                    SET selling_price = @SellingPrice
                    WHERE medicine_id = @Id AND remaining_units > 0";

                await connection.ExecuteAsync(globalPriceUpdateQuery, medicine, transaction);

                const string latestBatchUpdateQuery = @"
                    UPDATE inventory_batches 
                    SET unit_cost = @PurchasePrice, 
                        purchase_total_price = @PurchasePrice * quantity_units,
                        remaining_units = @StockQty,
                        expiry_date = @ExpiryDate,
                        supplier_id = @supId
                    WHERE medicine_id = @medId 
                    AND id = (SELECT id FROM inventory_batches WHERE medicine_id = @medId ORDER BY id DESC LIMIT 1)";

                await connection.ExecuteAsync(latestBatchUpdateQuery, new 
                { 
                    medId = medicine.Id,
                    medicine.PurchasePrice,
                    medicine.StockQty,
                    ExpiryDate = medicine.ExpiryDate ?? DateTime.Now.AddYears(1),
                    supId = supplierId
                }, transaction);

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError($"MedicineRepository.UpdateAsync failed for medicine {medicine.Id}", ex);
                throw new DataAccessException("Could not update medicine details. Please try again.", ex);
            }
            _eventBus.Publish(InventoryChangeType.MedicineUpdated);
        }

        public async Task DeleteAsync(int id)
        {
            _auth.EnforceAdmin();
            const string query = "DELETE FROM medicines WHERE id = @id";
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync(query, new { id });
            _eventBus.Publish(InventoryChangeType.MedicineDeleted);
        }

        public async Task DeleteBulkAsync(IEnumerable<int> ids)
        {
            _auth.EnforceAdmin();
            if (ids == null || !ids.Any()) return;

            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();
            
            try
            {
                const string query = "DELETE FROM medicines WHERE id = ANY(@ids)";
                await connection.ExecuteAsync(query, new { ids = ids.ToArray() }, transaction);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("MedicineRepository.DeleteBulkAsync failed", ex);
                throw new DataAccessException("Could not delete selected medicines. Ensure they are not tied to existing sales.", ex);
            }

            _eventBus.Publish(InventoryChangeType.MedicineDeleted);
        }
    }
}
