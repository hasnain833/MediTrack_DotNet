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
        Task<List<DashboardSaleItem>> GetRecentSalesAsync(int limit = 15);
        Task<List<DashboardMedicineAlert>> GetLowStockItemsAsync(int threshold = 10, int limit = 15);
        Task<List<DashboardMedicineAlert>> GetExpiringItemsAsync(int days = 30, int limit = 15);
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

        public async Task<List<DashboardMedicineAlert>> GetLowStockItemsAsync(int threshold = 10, int limit = 15)
        {
            const string query = @"
                SELECT 
                    m.name as Name,
                    COALESCE(SUM(b.remaining_units), 0) || ' units left' as SubText
                FROM medicines m
                LEFT JOIN inventory_batches b ON m.id = b.medicine_id
                GROUP BY m.id, m.name
                HAVING COALESCE(SUM(b.remaining_units), 0) < @threshold
                ORDER BY COALESCE(SUM(b.remaining_units), 0) ASC
                LIMIT @limit";
            
            using var conn = _db.GetConnection();
            var results = await conn.QueryAsync<DashboardMedicineAlert>(query, new { threshold, limit });
            return results.ToList();
        }

        public async Task<List<DashboardMedicineAlert>> GetExpiringItemsAsync(int days = 30, int limit = 15)
        {
            string query = $@"
                SELECT 
                    m.name as Name,
                    'Expires: ' || TO_CHAR(b.expiry_date, 'YYYY-MM-DD') as SubText
                FROM medicines m
                JOIN inventory_batches b ON m.id = b.medicine_id
                WHERE b.expiry_date <= CURRENT_DATE + INTERVAL '{days} days'
                AND b.remaining_units > 0
                ORDER BY b.expiry_date ASC
                LIMIT @limit";
            
            using var conn = _db.GetConnection();
            var results = await conn.QueryAsync<DashboardMedicineAlert>(query, new { limit });
            return results.ToList();
        }
    }

    public class DashboardSaleItem
    {
        public string Invoice { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public string Method { get; set; } = string.Empty;
    }

    public class DashboardMedicineAlert
    {
        public string Name { get; set; } = string.Empty;
        public string SubText { get; set; } = string.Empty;
    }
}
