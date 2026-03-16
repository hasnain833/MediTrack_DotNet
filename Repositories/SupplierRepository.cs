using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Services;
using Dapper;

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
            const string query = "SELECT id, name, phone, address, created_at as CreatedAt FROM suppliers ORDER BY name ASC";
            using var conn = _db.GetConnection();
            var suppliers = await conn.QueryAsync<Supplier>(query);
            return suppliers.ToList();
        }

        public async Task AddAsync(Supplier supplier)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO suppliers (name, phone, address) VALUES (@Name, @Phone, @Address) RETURNING id";
            using var conn = _db.GetConnection();
            supplier.Id = await conn.ExecuteScalarAsync<int>(query, new { supplier.Name, supplier.Phone, supplier.Address });
        }

        public async Task<Supplier> GetOrCreateByNameAsync(string name)
        {
            const string query = "SELECT id, name, phone, address, created_at as CreatedAt FROM suppliers WHERE LOWER(name) = LOWER(@name) LIMIT 1";
            using var conn = _db.GetConnection();
            var existing = await conn.QuerySingleOrDefaultAsync<Supplier>(query, new { name = name.Trim() });
            
            if (existing != null) return existing;

            var @new = new Supplier { Name = name.Trim() };
            await AddAsync(@new);
            return @new;
        }
    }
}
