using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MySql.Data.MySqlClient;

namespace MediTrack.Repositories
{
    public class MedicineRepository
    {
        private readonly DatabaseService _db;

        public MedicineRepository(DatabaseService db)
        {
            _db = db;
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

        private static Medicine MapMedicine(MySqlDataReader reader)
        {
            return new Medicine
            {
                Id = Convert.ToInt32(reader["id"]),
                MedicineName = reader["medicine_name"].ToString() ?? string.Empty,
                Category = reader["category"].ToString() ?? string.Empty,
                Price = Convert.ToDecimal(reader["price"]),
                StockQty = Convert.ToInt32(reader["stock_qty"]),
                ExpiryDate = Convert.ToDateTime(reader["expiry_date"]),
                Supplier = reader["supplier"].ToString() ?? string.Empty
            };
        }
    }
}
