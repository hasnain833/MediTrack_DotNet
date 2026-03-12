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
                    c.name as category_name, 
                    man.name as manufacturer_name,
                    s.name as supplier_name,
                    COALESCE(SUM(b.stock_qty), 0) as total_stock,
                    MAX(b.selling_price) as latest_price,
                    MIN(b.purchase_price) as cost_price,
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                LEFT JOIN suppliers s ON b.supplier_id = s.id
                GROUP BY m.id, c.name, man.name, s.name
                ORDER BY m.name ASC";
            try
            {
                return await _db.FetchAllAsync(query, MapMedicine);
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
                    c.name as category_name, 
                    man.name as manufacturer_name,
                    s.name as supplier_name,
                    COALESCE(SUM(b.stock_qty), 0) as total_stock,
                    MAX(b.selling_price) as latest_price,
                    MIN(b.purchase_price) as cost_price,
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                LEFT JOIN suppliers s ON b.supplier_id = s.id
                WHERE m.name ILIKE @text OR m.generic_name ILIKE @text OR m.barcode = @exact
                GROUP BY m.id, c.name, man.name, s.name";
            var parameters = new Dictionary<string, object>
            {
                { "@text", $"%{text}%" },
                { "@exact", text }
            };
            try
            {
                return await _db.FetchAllAsync(query, MapMedicine, parameters);
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
                    c.name as category_name, 
                    man.name as manufacturer_name,
                    COALESCE(SUM(b.stock_qty), 0) as total_stock,
                    MAX(b.selling_price) as latest_price,
                    MIN(b.purchase_price) as cost_price,
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE m.barcode = @barcode 
                GROUP BY m.id, c.name, man.name
                LIMIT 1";
            var parameters = new Dictionary<string, object> { { "@barcode", barcode } };
            try
            {
                return await _db.FetchOneAsync(query, MapMedicine, parameters);
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"MedicineRepository.GetByBarcodeAsync failed for barcode '{barcode}'", ex);
                throw new DataAccessException("Could not look up medicine by barcode.", ex);
            }
        }        private static Medicine MapMedicine(NpgsqlDataReader reader)
        {
            return new Medicine
            {
                Id              = Convert.ToInt32(reader["id"]),
                Name            = reader["name"].ToString() ?? string.Empty,
                GenericName     = reader["generic_name"] != DBNull.Value ? reader["generic_name"].ToString() : null,
                CategoryId      = reader["category_id"] != DBNull.Value ? Convert.ToInt32(reader["category_id"]) : null,
                ManufacturerId  = reader["manufacturer_id"] != DBNull.Value ? Convert.ToInt32(reader["manufacturer_id"]) : null,
                DosageForm      = reader["dosage_form"] != DBNull.Value ? reader["dosage_form"].ToString() : null,
                Strength        = reader["strength"] != DBNull.Value ? reader["strength"].ToString() : null,
                Barcode         = reader["barcode"].ToString() ?? string.Empty,
                CreatedAt       = Convert.ToDateTime(reader["created_at"]),
                // Multi-unit packaging
                BaseUnit        = reader["base_unit"] != DBNull.Value ? reader["base_unit"].ToString()! : "unit",
                StripSize       = reader["strip_size"] != DBNull.Value ? Convert.ToInt32(reader["strip_size"]) : null,
                BoxSize         = reader["box_size"] != DBNull.Value ? Convert.ToInt32(reader["box_size"]) : null,
                // Joined aggregates
                CategoryName    = reader["category_name"] != DBNull.Value ? reader["category_name"].ToString() : null,
                ManufacturerName = reader["manufacturer_name"] != DBNull.Value ? reader["manufacturer_name"].ToString() : null,
                SupplierName    = reader["supplier_name"] != DBNull.Value ? reader["supplier_name"].ToString() : null,
                StockQty        = reader["total_stock"] != DBNull.Value ? Convert.ToInt32(reader["total_stock"]) : 0,
                SellingPrice    = reader["latest_price"] != DBNull.Value ? Convert.ToDecimal(reader["latest_price"]) : 0,
                PurchasePrice   = reader["cost_price"] != DBNull.Value ? Convert.ToDecimal(reader["cost_price"]) : 0,
                ExpiryDate      = reader["earliest_expiry"] != DBNull.Value ? Convert.ToDateTime(reader["earliest_expiry"]) : null
            };
        }

        public async Task AddAsync(Medicine medicine)
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
                    INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode, base_unit, strip_size, box_size)
                    VALUES (@name, @generic, @catId, @manId, @dosage, @strength, @barcode, @baseUnit, @stripSize, @boxSize)
                    RETURNING id;";

                using var medCmd = new NpgsqlCommand(medQuery, connection, transaction);
                medCmd.Parameters.AddWithValue("@name", medicine.Name);
                medCmd.Parameters.AddWithValue("@generic", medicine.GenericName ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@catId", medicine.CategoryId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@manId", medicine.ManufacturerId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@dosage", medicine.DosageForm ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@strength", medicine.Strength ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@barcode", medicine.Barcode);
                medCmd.Parameters.AddWithValue("@baseUnit", string.IsNullOrWhiteSpace(medicine.BaseUnit) ? "unit" : medicine.BaseUnit);
                medCmd.Parameters.AddWithValue("@stripSize", medicine.StripSize.HasValue ? (object)medicine.StripSize.Value : DBNull.Value);
                medCmd.Parameters.AddWithValue("@boxSize", medicine.BoxSize.HasValue ? (object)medicine.BoxSize.Value : DBNull.Value);

                int medId = Convert.ToInt32(await medCmd.ExecuteScalarAsync());

                // 3. Insert Initial Batch
                const string batchQuery = @"
                    INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, expiry_date)
                    VALUES (@medId, @supId, @batchNo, @pPrice, @sPrice, @qty, @expiry)";

                using var batchCmd = new NpgsqlCommand(batchQuery, connection, transaction);
                batchCmd.Parameters.AddWithValue("@medId", medId);
                batchCmd.Parameters.AddWithValue("@supId", supplierId ?? (object)DBNull.Value);
                batchCmd.Parameters.AddWithValue("@batchNo", "BATCH-" + DateTime.Now.ToString("yyyyMMdd"));
                batchCmd.Parameters.AddWithValue("@pPrice", medicine.PurchasePrice);
                batchCmd.Parameters.AddWithValue("@sPrice", medicine.SellingPrice);
                batchCmd.Parameters.AddWithValue("@qty", medicine.StockQty);
                batchCmd.Parameters.AddWithValue("@expiry", medicine.ExpiryDate ?? (object)DateTime.Now.AddYears(1));

                await batchCmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("MedicineRepository.AddAsync failed", ex);
                throw new DataAccessException("Could not add medicine. Please try again.", ex);
            }

            _eventBus.Publish(InventoryChangeType.MedicineAdded);
        }

        private async Task<int> GetOrCreateCategoryAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans)
        {
            using var cmd = new NpgsqlCommand("SELECT id FROM categories WHERE LOWER(name) = LOWER(@name) LIMIT 1", conn, trans);
            cmd.Parameters.AddWithValue("@name", name);
            var id = await cmd.ExecuteScalarAsync();
            if (id != null) return Convert.ToInt32(id);

            using var insCmd = new NpgsqlCommand("INSERT INTO categories (name) VALUES (@name) RETURNING id", conn, trans);
            insCmd.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(await insCmd.ExecuteScalarAsync());
        }

        private async Task<int> GetOrCreateManufacturerAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans)
        {
            using var cmd = new NpgsqlCommand("SELECT id FROM manufacturers WHERE LOWER(name) = LOWER(@name) LIMIT 1", conn, trans);
            cmd.Parameters.AddWithValue("@name", name);
            var id = await cmd.ExecuteScalarAsync();
            if (id != null) return Convert.ToInt32(id);

            using var insCmd = new NpgsqlCommand("INSERT INTO manufacturers (name) VALUES (@name) RETURNING id", conn, trans);
            insCmd.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(await insCmd.ExecuteScalarAsync());
        }

        private async Task<int> GetOrCreateSupplierAsync(string name, NpgsqlConnection conn, NpgsqlTransaction trans)
        {
            using var cmd = new NpgsqlCommand("SELECT id FROM suppliers WHERE LOWER(name) = LOWER(@name) LIMIT 1", conn, trans);
            cmd.Parameters.AddWithValue("@name", name);
            var id = await cmd.ExecuteScalarAsync();
            if (id != null) return Convert.ToInt32(id);

            using var insCmd = new NpgsqlCommand("INSERT INTO suppliers (name) VALUES (@name) RETURNING id", conn, trans);
            insCmd.Parameters.AddWithValue("@name", name);
            return Convert.ToInt32(await insCmd.ExecuteScalarAsync());
        }

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
                    SET name = @name, generic_name = @generic, category_id = @catId, 
                        manufacturer_id = @manId, dosage_form = @dosage, strength = @strength, barcode = @barcode,
                        base_unit = @baseUnit, strip_size = @stripSize, box_size = @boxSize
                    WHERE id = @id";
                
                using var medCmd = new NpgsqlCommand(medQuery, connection, transaction);
                medCmd.Parameters.AddWithValue("@id", medicine.Id);
                medCmd.Parameters.AddWithValue("@name", medicine.Name);
                medCmd.Parameters.AddWithValue("@generic", medicine.GenericName ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@catId", medicine.CategoryId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@manId", medicine.ManufacturerId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@dosage", medicine.DosageForm ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@strength", medicine.Strength ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@barcode", medicine.Barcode);
                medCmd.Parameters.AddWithValue("@baseUnit", string.IsNullOrWhiteSpace(medicine.BaseUnit) ? "unit" : medicine.BaseUnit);
                medCmd.Parameters.AddWithValue("@stripSize", medicine.StripSize.HasValue ? (object)medicine.StripSize.Value : DBNull.Value);
                medCmd.Parameters.AddWithValue("@boxSize", medicine.BoxSize.HasValue ? (object)medicine.BoxSize.Value : DBNull.Value);
                await medCmd.ExecuteNonQueryAsync();

                // 3. Update Inventory Batch (Price and Quantity)
                // We update the most recent batch for this medicine to reflect the changes made in the 'Edit' dialog
                const string batchUpdateQuery = @"
                    UPDATE inventory_batches 
                    SET selling_price = @sPrice, 
                        purchase_price = @pPrice, 
                        stock_qty = @qty,
                        expiry_date = @expiry,
                        supplier_id = @supId
                    WHERE medicine_id = @medId 
                    AND id = (SELECT id FROM inventory_batches WHERE medicine_id = @medId ORDER BY id DESC LIMIT 1)";

                using var batchCmd = new NpgsqlCommand(batchUpdateQuery, connection, transaction);
                batchCmd.Parameters.AddWithValue("@medId", medicine.Id);
                batchCmd.Parameters.AddWithValue("@sPrice", medicine.SellingPrice);
                batchCmd.Parameters.AddWithValue("@pPrice", medicine.PurchasePrice);
                batchCmd.Parameters.AddWithValue("@qty", medicine.StockQty);
                batchCmd.Parameters.AddWithValue("@expiry", medicine.ExpiryDate ?? (object)DateTime.Now.AddYears(1));
                batchCmd.Parameters.AddWithValue("@supId", supplierId ?? (object)DBNull.Value);
                await batchCmd.ExecuteNonQueryAsync();

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
            var parameters = new Dictionary<string, object> { { "@id", id } };
            await _db.ExecuteNonQueryAsync(query, parameters);

            // Notify all screens about the deleted medicine
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
                using var cmd = new NpgsqlCommand(query, connection, transaction);
                cmd.Parameters.AddWithValue("ids", ids.ToArray());
                await cmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("MedicineRepository.DeleteBulkAsync failed", ex);
                throw new DataAccessException("Could not delete selected medicines. Ensure they are not tied to existing sales.", ex);
            }

            // Notify all screens about the deleted medicines
            _eventBus.Publish(InventoryChangeType.MedicineDeleted);
        }
    }
}
