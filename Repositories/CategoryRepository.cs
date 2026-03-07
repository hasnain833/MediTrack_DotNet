using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Npgsql;

namespace MediTrack.Repositories
{
    public class CategoryRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public CategoryRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<Category>> GetAllAsync()
        {
            const string query = "SELECT * FROM categories ORDER BY name ASC";
            return await _db.FetchAllAsync(query, MapCategory);
        }

        private static Category MapCategory(NpgsqlDataReader reader)
        {
            return new Category
            {
                Id = Convert.ToInt32(reader["id"]),
                Name = reader["name"].ToString() ?? string.Empty,
                CreatedAt = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(Category category)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO categories (name) VALUES (@name)";
            var parameters = new Dictionary<string, object> { { "@name", category.Name } };
            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
