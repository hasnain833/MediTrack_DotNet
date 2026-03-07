using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Npgsql;


namespace MediTrack.Repositories
{
    public class MedicineRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public MedicineRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<Medicine>> GetAllAsync()
        {
            const string query = @"
                SELECT 
                    m.*, 
                    c.name as category_name, 
                    man.name as manufacturer_name,
                    COALESCE(SUM(b.stock_qty), 0) as total_stock,
                    MAX(b.selling_price) as latest_price,
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                GROUP BY m.id, c.name, man.name
                ORDER BY m.name ASC";
            return await _db.FetchAllAsync(query, MapMedicine);
        }

        public async Task<List<Medicine>> SearchAsync(string text)
        {
            const string query = @"
                SELECT 
                    m.*, 
                    c.name as category_name, 
                    man.name as manufacturer_name,
                    COALESCE(SUM(b.stock_qty), 0) as total_stock,
                    MAX(b.selling_price) as latest_price,
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE m.name LIKE @text OR m.generic_name LIKE @text OR m.barcode = @exact
                GROUP BY m.id, c.name, man.name";
            
            var parameters = new Dictionary<string, object>
            {
                { "@text", $"%{text}%" },
                { "@exact", text }
            };
            return await _db.FetchAllAsync(query, MapMedicine, parameters);
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
                    MIN(b.expiry_date) as earliest_expiry
                FROM medicines m
                LEFT JOIN categories c ON m.category_id = c.id
                LEFT JOIN manufacturers man ON m.manufacturer_id = man.id
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE m.barcode = @barcode 
                GROUP BY m.id, c.name, man.name
                LIMIT 1";
            var parameters = new Dictionary<string, object> { { "@barcode", barcode } };
            return await _db.FetchOneAsync(query, MapMedicine, parameters);
        }

        private static Medicine MapMedicine(NpgsqlDataReader reader)
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
                CategoryName    = reader["category_name"] != DBNull.Value ? reader["category_name"].ToString() : null,
                ManufacturerName = reader["manufacturer_name"] != DBNull.Value ? reader["manufacturer_name"].ToString() : null,
                StockQty        = reader["total_stock"] != DBNull.Value ? Convert.ToInt32(reader["total_stock"]) : 0,
                Price           = reader["latest_price"] != DBNull.Value ? Convert.ToDecimal(reader["latest_price"]) : 0,
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
                // 1. Insert Medicine
                const string medQuery = @"
                    INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode)
                    VALUES (@name, @generic, @catId, @manId, @dosage, @strength, @barcode)
                    RETURNING id;";

                using var medCmd = new NpgsqlCommand(medQuery, connection, transaction);
                medCmd.Parameters.AddWithValue("@name", medicine.Name);
                medCmd.Parameters.AddWithValue("@generic", medicine.GenericName ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@catId", medicine.CategoryId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@manId", medicine.ManufacturerId ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@dosage", medicine.DosageForm ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@strength", medicine.Strength ?? (object)DBNull.Value);
                medCmd.Parameters.AddWithValue("@barcode", medicine.Barcode);

                int medId = Convert.ToInt32(await medCmd.ExecuteScalarAsync());

                // 2. Insert Initial Batch (using current UI values if provided)
                const string batchQuery = @"
                    INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, expiry_date)
                    VALUES (@medId, @supId, @batchNo, @pPrice, @sPrice, @qty, @expiry)";

                using var batchCmd = new NpgsqlCommand(batchQuery, connection, transaction);
                batchCmd.Parameters.AddWithValue("@medId", medId);
                batchCmd.Parameters.AddWithValue("@supId", DBNull.Value); // Default supplier null
                batchCmd.Parameters.AddWithValue("@batchNo", "BATCH-001-NEW");
                batchCmd.Parameters.AddWithValue("@pPrice", medicine.Price * 0.8m); // Estimate purchase price
                batchCmd.Parameters.AddWithValue("@sPrice", medicine.Price);
                batchCmd.Parameters.AddWithValue("@qty", medicine.StockQty);
                batchCmd.Parameters.AddWithValue("@expiry", medicine.ExpiryDate ?? (object)DateTime.Now.AddYears(1));

                await batchCmd.ExecuteNonQueryAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task UpdateAsync(Medicine medicine)
        {
            _auth.EnforceAdmin();
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // 1. Update Medicine Metadata
                const string medQuery = @"
                    UPDATE medicines 
                    SET name = @name, generic_name = @generic, category_id = @catId, 
                        manufacturer_id = @manId, dosage_form = @dosage, strength = @strength, barcode = @barcode
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
                await medCmd.ExecuteNonQueryAsync();

                // 2. Update the 'latest' batch (simplified for UI logic)
                const string batchQuery = @"
                    UPDATE inventory_batches 
                    SET selling_price = @price, stock_qty = @qty, expiry_date = @expiry
                    WHERE medicine_id = @medId 
                    AND id = (SELECT id FROM inventory_batches WHERE medicine_id = @medId ORDER BY created_at DESC LIMIT 1)";

                using var batchCmd = new NpgsqlCommand(batchQuery, connection, transaction);
                batchCmd.Parameters.AddWithValue("@medId", medicine.Id);
                batchCmd.Parameters.AddWithValue("@price", medicine.Price);
                batchCmd.Parameters.AddWithValue("@qty", medicine.StockQty);
                batchCmd.Parameters.AddWithValue("@expiry", medicine.ExpiryDate ?? (object)DateTime.Now.AddYears(1));
                await batchCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            _auth.EnforceAdmin();
            const string query = "DELETE FROM medicines WHERE id = @id";
            var parameters = new Dictionary<string, object> { { "@id", id } };
            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
