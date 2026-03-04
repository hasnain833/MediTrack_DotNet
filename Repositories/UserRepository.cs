using System;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MySql.Data.MySqlClient;

namespace MediTrack.Repositories
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
            const string query = "SELECT * FROM users WHERE username = @username LIMIT 1";
            var parameters = new System.Collections.Generic.Dictionary<string, object>
            {
                { "@username", username }
            };

            return await _db.FetchOneAsync(query, MapUser, parameters);
        }

        private static User MapUser(MySqlDataReader reader)
        {
            return new User
            {
                Id = Convert.ToInt32(reader["id"]),
                Username = reader["username"].ToString() ?? string.Empty,
                Password = reader["password"].ToString() ?? string.Empty,
                FullName = reader["full_name"].ToString() ?? string.Empty,
                Role = reader["role"].ToString() ?? string.Empty,
                Status = reader["status"].ToString() ?? "Active"
            };
        }
    }
}
