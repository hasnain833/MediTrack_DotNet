using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Npgsql;

namespace MediTrack.Repositories
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
            const string query = "SELECT * FROM manufacturers ORDER BY name ASC";
            return await _db.FetchAllAsync(query, MapManufacturer);
        }

        private static Manufacturer MapManufacturer(NpgsqlDataReader reader)
        {
            return new Manufacturer
            {
                Id = Convert.ToInt32(reader["id"]),
                Name = reader["name"].ToString() ?? string.Empty,
                CreatedAt = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(Manufacturer manufacturer)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO manufacturers (name) VALUES (@name)";
            var parameters = new Dictionary<string, object> { { "@name", manufacturer.Name } };
            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
