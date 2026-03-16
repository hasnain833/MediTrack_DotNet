using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Utils;
using Dapper;

namespace DChemist.Repositories
{
    public class UserRepository
    {
        private readonly DatabaseService _db;

        public UserRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            const string query = @"
                SELECT 
                    id, username, password, 
                    full_name as FullName, 
                    role, status, 
                    must_change_password as MustChangePassword 
                FROM users 
                WHERE username = @username 
                LIMIT 1";

            using var conn = _db.GetConnection();
            return await conn.QuerySingleOrDefaultAsync<User>(query, new { username });
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            const string query = @"
                SELECT 
                    id, username, password, 
                    full_name as FullName, 
                    role, status, 
                    must_change_password as MustChangePassword 
                FROM users";

            using var conn = _db.GetConnection();
            var users = await conn.QueryAsync<User>(query);
            return users.ToList();
        }

        public async Task UpdatePasswordAsync(int userId, string newHashedPassword)
        {
            const string query = "UPDATE users SET password = @password, must_change_password = FALSE WHERE id = @id";
            using var conn = _db.GetConnection();
            await conn.ExecuteAsync(query, new { id = userId, password = newHashedPassword });
        }
    }
}
