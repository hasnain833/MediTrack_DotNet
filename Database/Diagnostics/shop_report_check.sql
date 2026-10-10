-- READ ONLY. Run in pgAdmin Query Tool against the SHOP database, after taking a backup.
-- Change the report date in each query if investigating a different day.
-- Do not run a reset, truncate, or a bulk cost correction based only on these results.

-- 1. Daily stored bill totals and the old cost calculation.
WITH bills AS (
    SELECT * FROM sales
    WHERE (sale_date AT TIME ZONE 'Asia/Karachi')::date = DATE '2026-10-10'
      AND status <> 'Voided'
), costs AS (
    SELECT SUM((si.quantity - si.returned_qty) * COALESCE(b.unit_cost, 0)) AS cost,
           SUM((si.quantity - si.returned_qty) * si.unit_price) AS item_revenue
    FROM sale_items si JOIN bills s ON s.id = si.sale_id
    LEFT JOIN inventory_batches b ON b.id = si.batch_id
)
SELECT COUNT(*) AS bill_count, COALESCE(SUM(grand_total), 0) AS stored_net_sales,
       COALESCE(SUM(tax_amount), 0) AS tax, COALESCE(SUM(discount_amount), 0) AS discounts,
       (SELECT cost FROM costs) AS current_batch_cost,
       (SELECT item_revenue - cost FROM costs) AS old_report_profit,
       COALESCE(SUM(grand_total - tax_amount), 0) - COALESCE((SELECT cost FROM costs), 0) AS profit_using_current_cost
FROM bills;

-- 2. Largest cost contributions. Compare these to the ORIGINAL supplier invoice.
-- total/quantity is only a consistency check, not an automatic historical correction.
SELECT s.bill_no, m.name AS medicine, b.id AS batch_id, b.batch_no,
       si.quantity, si.returned_qty, si.unit_price AS sale_price_per_unit,
       b.unit_cost AS stored_cost_per_unit,
       (si.quantity - si.returned_qty) * b.unit_cost AS cost_contribution,
       b.purchase_total_price, b.quantity_units, b.entry_mode,
       m.packets_per_box, m.units_per_pack,
       ROUND(b.purchase_total_price / NULLIF(b.quantity_units, 0), 6) AS total_divided_by_quantity,
       CASE WHEN b.unit_cost > si.unit_price THEN 'Cost exceeds sale price: verify invoice and unit scale'
            WHEN b.unit_cost IS NULL OR b.unit_cost <= 0 THEN 'Missing or zero cost'
            ELSE 'Check against original invoice' END AS review
FROM sales s JOIN sale_items si ON si.sale_id = s.id
LEFT JOIN medicines m ON m.id = si.medicine_id
LEFT JOIN inventory_batches b ON b.id = si.batch_id
WHERE (s.sale_date AT TIME ZONE 'Asia/Karachi')::date = DATE '2026-10-10'
  AND s.status <> 'Voided'
ORDER BY cost_contribution DESC NULLS LAST;

-- 3. Bill totals that disagree with their stored quantities and unit prices.
SELECT s.bill_no, s.status, s.grand_total AS saved_bill_total,
       SUM((si.quantity - si.returned_qty) * si.unit_price) AS retained_item_amount,
       SUM((si.quantity - si.returned_qty) * si.unit_price) + s.tax_amount - s.discount_amount
           + MAX(COALESCE((to_jsonb(s)->>'extra_amount')::numeric, 0)) AS calculated_bill_total,
       s.grand_total - (SUM((si.quantity - si.returned_qty) * si.unit_price) + s.tax_amount - s.discount_amount
           + MAX(COALESCE((to_jsonb(s)->>'extra_amount')::numeric, 0))) AS difference
FROM sales s JOIN sale_items si ON si.sale_id = s.id
WHERE (s.sale_date AT TIME ZONE 'Asia/Karachi')::date = DATE '2026-10-10'
  AND s.status <> 'Voided'
GROUP BY s.id, s.bill_no, s.status, s.grand_total, s.tax_amount, s.discount_amount
HAVING ABS(s.grand_total - (SUM((si.quantity - si.returned_qty) * si.unit_price) + s.tax_amount - s.discount_amount
    + MAX(COALESCE((to_jsonb(s)->>'extra_amount')::numeric, 0)))) > 0.05
ORDER BY ABS(s.grand_total - (SUM((si.quantity - si.returned_qty) * si.unit_price) + s.tax_amount - s.discount_amount
    + MAX(COALESCE((to_jsonb(s)->>'extra_amount')::numeric, 0)))) DESC;
