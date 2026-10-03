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
            const string query = "SELECT CAST(COALESCE(SUM(grand_total), 0) AS numeric(20,2)) FROM sales WHERE sale_date >= CURRENT_DATE AND sale_date < CURRENT_DATE + 1";
            
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

    /// <summary>Dashboard figures added with the redesign. Voided bills never count.</summary>
    public class DashboardStatsRepository
    {
        private readonly DatabaseService _db;
        public DashboardStatsRepository(DatabaseService db) { _db = db; }

        public async Task<(decimal Sales, int Bills, decimal Profit, DateTime? LastBill)> GetTodayAsync()
        {
            const string sql = @"
                SELECT
                    COALESCE(SUM(s.grand_total), 0)        AS Sales,
                    CAST(COUNT(*) AS INTEGER)              AS Bills,
                    MAX(s.sale_date)                       AS LastBill,
                    COALESCE((
                        SELECT SUM((si.quantity - si.returned_qty) * (si.unit_price - COALESCE(ib.unit_cost, 0)))
                        FROM sale_items si
                        JOIN sales s2 ON s2.id = si.sale_id
                        LEFT JOIN inventory_batches ib ON ib.id = si.batch_id
                        WHERE s2.sale_date >= CURRENT_DATE AND s2.sale_date < CURRENT_DATE + 1 AND s2.status <> 'Voided'
                    ), 0)                                  AS Profit
                FROM sales s
                WHERE s.sale_date >= CURRENT_DATE AND s.sale_date < CURRENT_DATE + 1 AND s.status <> 'Voided'";
            using var conn = _db.GetConnection();
            var r = await conn.QuerySingleAsync(sql);
            return ((decimal)r.sales, (int)r.bills, (decimal)r.profit, (DateTime?)r.lastbill);
        }

        /// <summary>Net sales per day for the last 14 days (oldest first), zero-filled.</summary>
        public async Task<List<(DateTime Day, decimal Total)>> GetDailySalesAsync()
        {
            const string sql = @"
                SELECT d::date AS Day, COALESCE(SUM(s.grand_total), 0) AS Total
                FROM generate_series(CURRENT_DATE - 13, CURRENT_DATE, interval '1 day') d
                LEFT JOIN sales s ON s.sale_date >= d AND s.sale_date < d + interval '1 day' AND s.status <> 'Voided'
                GROUP BY d ORDER BY d";
            using var conn = _db.GetConnection();
            var rows = await conn.QueryAsync<(DateTime, decimal)>(sql);
            return rows.ToList();
        }

        /// <summary>Expired / expiring within 30 days (with stock left) and low stock (under one box, or under 10 units).</summary>
        public async Task<List<AttentionItem>> GetAttentionItemsAsync()
        {
            const string sql = @"
                SELECT m.name AS Name, 'exp' AS Kind, (b.expiry_date - CURRENT_DATE) AS DaysLeft,
                       b.remaining_units AS Units, b.expiry_date AS ExpiryDate
                FROM inventory_batches b JOIN medicines m ON m.id = b.medicine_id
                WHERE b.remaining_units > 0 AND b.expiry_date <= CURRENT_DATE + 30
                UNION ALL
                SELECT m.name, 'low', NULL, COALESCE(SUM(b.remaining_units), 0), NULL
                FROM medicines m LEFT JOIN inventory_batches b ON b.medicine_id = m.id
                GROUP BY m.id, m.name, m.packets_per_box, m.units_per_pack
                HAVING COALESCE(SUM(b.remaining_units), 0) < GREATEST(10, GREATEST(m.packets_per_box, 1) * GREATEST(m.units_per_pack, 1))
                ORDER BY 2, 3 NULLS LAST, 4
                LIMIT 200";
            using var conn = _db.GetConnection();
            var rows = await conn.QueryAsync<AttentionItem>(sql);
            return rows.ToList();
        }
    }

    public class AttentionItem
    {
        public string Name { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;   // exp / low
        public int? DaysLeft { get; set; }
        public int Units { get; set; }
        public DateTime? ExpiryDate { get; set; }

        public bool IsExpiry => Kind == "exp";
        public string Tag => !IsExpiry ? "Low stock" : DaysLeft < 0 ? "Expired" : DaysLeft == 0 ? "Today" : $"{DaysLeft} days";
        /// <summary>Red: expired or ≤14 days. Amber: later expiry or low stock.</summary>
        public bool IsUrgent => IsExpiry && DaysLeft <= 14;
        public string Info => IsExpiry
            ? $"{(DaysLeft < 0 ? "Expired" : "Expires")} {ExpiryDate:d MMM yyyy} · {Units} units"
            : Units == 0 ? "Out of stock" : $"{Units} units left";
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
