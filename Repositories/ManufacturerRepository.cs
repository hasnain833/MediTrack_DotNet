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
    public class ManufacturerRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public ManufacturerRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<Manufacturer>> GetAllAsync()
        {
            const string query = "SELECT id, name, created_at as CreatedAt FROM manufacturers ORDER BY name ASC";
            using var conn = _db.GetConnection();
            var manufacturers = await conn.QueryAsync<Manufacturer>(query);
            return manufacturers.ToList();
        }

        public async Task AddAsync(Manufacturer manufacturer)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO manufacturers (name) VALUES (@Name)";
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync(query, new { manufacturer.Name });
        }
    }
}
