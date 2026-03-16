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
            const string query = "SELECT id, name, created_at as CreatedAt FROM categories ORDER BY name ASC";
            using var conn = _db.GetConnection();
            var categories = await conn.QueryAsync<Category>(query);
            return categories.ToList();
        }

        public async Task AddAsync(Category category)
        {
            _auth.EnforceAdmin();
            const string query = "INSERT INTO categories (name) VALUES (@Name)";
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync(query, new { category.Name });
        }
    }
}
