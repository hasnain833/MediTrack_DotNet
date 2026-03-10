using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Utils;
using Npgsql;

namespace DChemist.Repositories
{
    public class AuditRepository
    {
        private readonly DatabaseService _db;

        public AuditRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task InsertLogAsync(int userId, string action, string details)
        {
            try
            {
                const string query = @"
                    INSERT INTO audit_logs (user_id, action, details)
                    VALUES (@userId, @action, @details)";

                var parameters = new Dictionary<string, object>
                {
                    { "@userId", userId },
                    { "@action", action },
                    { "@details", details }
                };

                await _db.ExecuteNonQueryAsync(query, parameters);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to insert audit log", ex);
            }
        }

        public async Task<List<AuditLog>> GetLogsAsync(int limit = 100)
        {
            const string query = @"
                SELECT a.id, a.user_id, COALESCE(u.username, 'System') as username, a.action, a.details, a.created_at
                FROM audit_logs a
                LEFT JOIN users u ON a.user_id = u.id
                ORDER BY a.created_at DESC
                LIMIT @limit";

            var parameters = new Dictionary<string, object> { { "@limit", limit } };
            return await _db.FetchAllAsync(query, MapAuditLog, parameters);
        }

        private AuditLog MapAuditLog(NpgsqlDataReader reader)
        {
            return new AuditLog
            {
                Id = Convert.ToInt32(reader["id"]),
                UserId = reader["user_id"] != DBNull.Value ? Convert.ToInt32(reader["user_id"]) : 0,
                Username = reader["username"].ToString() ?? "System",
                Action = reader["action"].ToString() ?? "",
                Details = reader["details"].ToString() ?? "",
                CreatedAt = Convert.ToDateTime(reader["created_at"])
            };
        }
    }
}
