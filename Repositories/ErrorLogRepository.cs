using System;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Utils;
using Dapper;

namespace DChemist.Repositories
{
    public class ErrorLogRepository
    {
        private readonly DatabaseService _db;

        public ErrorLogRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task InsertErrorAsync(string message, string? stackTrace, string? source)
        {
            try
            {
                const string query = @"
                    INSERT INTO error_logs (message, stack_trace, source, created_at)
                    VALUES (@message, @stackTrace, @source, CURRENT_TIMESTAMP)";
                
                using var conn = _db.GetConnection();
                await conn.ExecuteAsync(query, new { message, stackTrace, source });
            }
            catch (Exception ex)
            {
                // Fallback to file logging if DB logging fails
                AppLogger.LogError("ErrorLogRepository.InsertErrorAsync failed", ex);
            }
        }
    }
}
