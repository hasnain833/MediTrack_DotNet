using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Database;
using Dapper;
using Npgsql;

namespace DChemist.Repositories
{
    public interface IDashboardRepository
    {
        Task<long> GetLowStockCountAsync(int threshold = 10);
        Task<long> GetExpiringSoonCountAsync(int days = 30);
        Task<decimal> GetTodaysRevenueAsync();
        Task<List<DashboardSaleItem>> GetRecentSalesAsync(int limit = 5);
        Task<List<CriticalAlert>> GetCriticalAlertsAsync();
    }

    public class DashboardRepository : IDashboardRepository
    {
        private readonly DatabaseService _db;

        public DashboardRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<long> GetLowStockCountAsync(int threshold = 10)
        {
            const string query = @"
                SELECT COUNT(*) FROM (
                    SELECT medicine_id FROM inventory_batches
                    GROUP BY medicine_id
                    HAVING COALESCE(SUM(remaining_units), 0) < @threshold
                ) AS low_stock";
            
            using var conn = _db.GetConnection();
            return await conn.ExecuteScalarAsync<long>(query, new { threshold });
        }

        public async Task<long> GetExpiringSoonCountAsync(int days = 30)
        {
            string query = $@"
                SELECT COUNT(*) FROM inventory_batches 
                WHERE expiry_date <= CURRENT_DATE + INTERVAL '{days} days' 
                AND remaining_units > 0";
            
            using var conn = _db.GetConnection();
            return await conn.ExecuteScalarAsync<long>(query);
        }

        public async Task<decimal> GetTodaysRevenueAsync()
        {
            const string query = "SELECT CAST(COALESCE(SUM(grand_total), 0) AS numeric(20,2)) FROM sales WHERE sale_date::date = CURRENT_DATE";
            
            using var conn = _db.GetConnection();
            return await conn.ExecuteScalarAsync<decimal>(query);
        }

        public async Task<List<DashboardSaleItem>> GetRecentSalesAsync(int limit = 5)
        {
            string query = $@"
                SELECT 
                    bill_no AS Invoice, 
                    sale_date AS Date, 
                    grand_total AS Total,
                    'Cash' AS Method
                FROM sales 
                ORDER BY sale_date DESC 
                LIMIT @limit";

            using var conn = _db.GetConnection();
            var results = await conn.QueryAsync<DashboardSaleItem>(query, new { limit });
            return results.ToList();
        }

        public async Task<List<CriticalAlert>> GetCriticalAlertsAsync()
        {
            using var conn = _db.GetConnection();
            
            // 1. Low stock alerts
            const string lowStockQuery = @"
                SELECT 
                    m.name || ' is low in stock (' || SUM(b.remaining_units) || ' units)' as Message,
                    'Low Stock' as Type
                FROM medicines m
                JOIN inventory_batches b ON m.id = b.medicine_id
                GROUP BY m.name
                HAVING SUM(b.remaining_units) < @threshold
                LIMIT 5";
            
            var lowStockAlerts = await conn.QueryAsync<CriticalAlert>(lowStockQuery, new { threshold = 10 });

            // 2. Expiry alerts (next 30 days)
            const string expiryQuery = @"
                SELECT 
                    m.name || ' (Batch: ' || b.batch_no || ') expires on ' || TO_CHAR(b.expiry_date, 'YYYY-MM-DD') as Message,
                    'Expiry' as Type
                FROM medicines m
                JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE b.expiry_date <= CURRENT_DATE + INTERVAL '30 days'
                AND b.remaining_units > 0
                LIMIT 5";
            
            var expiryAlerts = await conn.QueryAsync<CriticalAlert>(expiryQuery);

            return lowStockAlerts.Concat(expiryAlerts).ToList();
        }
    }

    public class DashboardSaleItem
    {
        public string Invoice { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = string.Empty;
    }

    public class CriticalAlert
    {
        public string Message { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
