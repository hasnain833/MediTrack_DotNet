using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Utils;
using Dapper;
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

        public async Task InsertLogAsync(int userId, string action, string details, NpgsqlConnection? conn = null, NpgsqlTransaction? trans = null)
        {
            try
            {
                const string query = @"
                    INSERT INTO audit_logs (user_id, action, details)
                    VALUES (@userId, @action, @details)";

                if (conn != null && trans != null)
                {
                    await conn.ExecuteAsync(query, new { userId, action, details }, trans);
                }
                else
                {
                    using var connection = _db.GetConnection();
                    await connection.ExecuteAsync(query, new { userId, action, details });
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to insert audit log", ex);
            }
        }

        public async Task<List<AuditLog>> GetLogsAsync(int? userId = null, string? action = null, DateTime? date = null, int limit = 100)
        {
            var query = @"
                SELECT 
                    a.id, 
                    a.user_id as UserId, 
                    COALESCE(u.username, 'System') as Username, 
                    a.action, 
                    a.details, 
                    a.created_at as CreatedAt
                FROM audit_logs a
                LEFT JOIN users u ON a.user_id = u.id
                WHERE 1=1";

            var parameters = new DynamicParameters();
            parameters.Add("limit", limit);

            if (userId.HasValue)
            {
                query += " AND a.user_id = @userId";
                parameters.Add("userId", userId.Value);
            }
            if (!string.IsNullOrWhiteSpace(action))
            {
                query += " AND a.action = @action";
                parameters.Add("action", action);
            }
            if (date.HasValue)
            {
                query += " AND a.created_at::date = @date";
                parameters.Add("date", date.Value.Date);
            }

            query += " ORDER BY a.created_at DESC LIMIT @limit";

            using var conn = _db.GetConnection();
            var logs = await conn.QueryAsync<AuditLog>(query, parameters);
            return logs.ToList();
        }
    }
}
