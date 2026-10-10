using System;

namespace DChemist.Repositories
{
    /// <summary>One calculation for the dashboard and daily report. Refunds already reduce grand_total.</summary>
    internal static class FinancialReportQueries
    {
        public static object ForDay(DateTime day) => new
        {
            start = DateTime.SpecifyKind(day.Date, DateTimeKind.Local).ToUniversalTime(),
            end = DateTime.SpecifyKind(day.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime()
        };

        public const string Summary = @"
            WITH day_sales AS (
                SELECT * FROM sales WHERE sale_date >= @start AND sale_date < @end
            ), retained_cost AS (
                SELECT COALESCE(SUM((si.quantity - si.returned_qty) * COALESCE(si.unit_cost_at_sale, b.unit_cost, 0)), 0) AS cost,
                       COUNT(*) FILTER (WHERE si.quantity > si.returned_qty AND (COALESCE(si.unit_cost_at_sale, b.unit_cost) IS NULL OR COALESCE(si.unit_cost_at_sale, b.unit_cost) <= 0)) AS missing,
                       COUNT(*) FILTER (WHERE si.quantity > si.returned_qty AND si.unit_cost_at_sale IS NULL) AS estimated
                FROM sale_items si JOIN day_sales s ON s.id = si.sale_id
                LEFT JOIN inventory_batches b ON b.id = si.batch_id
                WHERE s.status <> 'Voided'
            ), refunds AS (
                SELECT COALESCE(SUM(si.returned_qty * si.unit_price), 0) AS amount,
                       COUNT(*) FILTER (WHERE si.returned_qty > 0) AS count
                FROM sale_items si JOIN day_sales s ON s.id = si.sale_id
            )
            SELECT CAST(COUNT(*) AS INTEGER) AS TotalSalesCount,
                   COALESCE(SUM(s.grand_total), 0) AS NetSales,
                   COALESCE(SUM(s.grand_total), 0) + (SELECT amount FROM refunds) AS GrossSales,
                   COALESCE(SUM(s.tax_amount), 0) AS TotalTax,
                   COALESCE(SUM(s.discount_amount), 0) AS TotalDiscount,
                   CAST(COUNT(*) FILTER (WHERE s.fbr_reported) AS INTEGER) AS FbrSalesCount,
                   CAST(COUNT(*) FILTER (WHERE NOT s.fbr_reported) AS INTEGER) AS InternalSalesCount,
                   (SELECT amount FROM refunds) AS TotalReturns,
                   CAST((SELECT count FROM refunds) AS INTEGER) AS ReturnsCount,
                   ROUND(COALESCE(SUM(s.grand_total - s.tax_amount), 0) - (SELECT cost FROM retained_cost), 2) AS TotalProfit,
                   CAST((SELECT missing FROM retained_cost) AS INTEGER) AS MissingCostItems,
                   CAST((SELECT estimated FROM retained_cost) AS INTEGER) AS EstimatedCostItems,
                   MAX(s.sale_date) AS LastBill
            FROM day_sales s WHERE s.status <> 'Voided'";
    }
}
