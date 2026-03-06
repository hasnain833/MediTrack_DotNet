using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Microsoft.Data.Sqlite;

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
            const string query = "SELECT * FROM inventory ORDER BY medicine_name ASC";
            return await _db.FetchAllAsync(query, MapMedicine);
        }

        public async Task<List<Medicine>> SearchAsync(string text)
        {
            const string query = "SELECT * FROM inventory WHERE medicine_name LIKE @text OR category LIKE @text";
            var parameters = new Dictionary<string, object>
            {
                { "@text", $"%{text}%" }
            };
            return await _db.FetchAllAsync(query, MapMedicine, parameters);
        }

        private static Medicine MapMedicine(SqliteDataReader reader)
        {
            return new Medicine
            {
                Id           = Convert.ToInt32(reader["id"]),
                MedicineName = reader["medicine_name"].ToString() ?? string.Empty,
                Category     = reader["category"].ToString() ?? string.Empty,
                Price        = Convert.ToDecimal(reader["price"]),
                StockQty     = Convert.ToInt32(reader["stock_qty"]),
                ExpiryDate   = DateTime.Parse(reader["expiry_date"].ToString() ?? DateTime.Now.ToString()),
                Supplier     = reader["supplier"].ToString() ?? string.Empty
            };
        }

        public async Task AddAsync(Medicine medicine)
        {
            _auth.EnforceAdmin();
            const string query = @"
                INSERT INTO inventory (medicine_name, category, price, stock_qty, expiry_date, supplier)
                VALUES (@name, @cat, @price, @qty, @expiry, @supplier)";
            
            var parameters = new Dictionary<string, object>
            {
                { "@name", medicine.MedicineName },
                { "@cat", medicine.Category },
                { "@price", medicine.Price },
                { "@qty", medicine.StockQty },
                { "@expiry", medicine.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "@supplier", medicine.Supplier }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }

        public async Task UpdateAsync(Medicine medicine)
        {
            _auth.EnforceAdmin();
            const string query = @"
                UPDATE inventory 
                SET medicine_name = @name, category = @cat, price = @price, 
                    stock_qty = @qty, expiry_date = @expiry, supplier = @supplier
                WHERE id = @id";
            
            var parameters = new Dictionary<string, object>
            {
                { "@id", medicine.Id },
                { "@name", medicine.MedicineName },
                { "@cat", medicine.Category },
                { "@price", medicine.Price },
                { "@qty", medicine.StockQty },
                { "@expiry", medicine.ExpiryDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "@supplier", medicine.Supplier }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }

        public async Task DeleteAsync(int id)
        {
            _auth.EnforceAdmin();
            const string query = "DELETE FROM inventory WHERE id = @id";
            var parameters = new Dictionary<string, object> { { "@id", id } };
            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
