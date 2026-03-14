using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Services;
using Npgsql;

namespace DChemist.Repositories
{
    public class SupplierRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public SupplierRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<Supplier>> GetAllAsync()
        {
            const string query = "SELECT * FROM suppliers ORDER BY name ASC";
            return await _db.FetchAllAsync(query, MapSupplier);
        }

        private static Supplier MapSupplier(NpgsqlDataReader reader)
        {
            return new Supplier
            {
                Id      = Convert.ToInt32(reader["id"]),
                Name    = reader["name"].ToString() ?? string.Empty,
                Phone   = reader["phone"] != DBNull.Value ? reader["phone"].ToString() : null,
                Address = reader["address"] != DBNull.Value ? reader["address"].ToString() : null,
                CreatedAt = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(Supplier supplier)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO suppliers (name, phone, address) VALUES (@name, @phone, @address) RETURNING id";
            var parameters = new Dictionary<string, object>
            {
                { "@name", supplier.Name },
                { "@phone", supplier.Phone ?? (object)DBNull.Value },
                { "@address", supplier.Address ?? (object)DBNull.Value }
            };
            var id = await _db.FetchOneAsync(query, r => Convert.ToInt32(r["id"]), parameters);
            supplier.Id = id;
        }

        public async Task<Supplier> GetOrCreateByNameAsync(string name)
        {
            const string query = "SELECT * FROM suppliers WHERE LOWER(name) = LOWER(@name) LIMIT 1";
            var parameters = new Dictionary<string, object> { { "@name", name.Trim() } };
            var existing = await _db.FetchOneAsync(query, MapSupplier, parameters);
            if (existing != null) return existing;

            var @new = new Supplier { Name = name.Trim() };
            await AddAsync(@new);
            return @new;
        }
    }
}
