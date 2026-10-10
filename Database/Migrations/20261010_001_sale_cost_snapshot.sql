-- Preserve the cost used for new sales, even when a batch is later restocked or edited.
-- Existing NULL values are deliberate: old sale-time costs cannot be recovered safely
-- from today's batch cost without checking the original purchase records.
ALTER TABLE sale_items ADD COLUMN IF NOT EXISTS unit_cost_at_sale NUMERIC(18,6);
