-- Migration: 20260606_001_performance_indexes.sql
-- Description: Create indexes to optimize database performance for sales items, suppliers, purchase invoices and active stock lookup.

-- Index for sale items -> batch join
CREATE INDEX IF NOT EXISTS idx_sale_items_batch_id ON sale_items(batch_id);

-- Index for purchase invoices -> supplier
CREATE INDEX IF NOT EXISTS idx_purchase_invoices_supplier_id ON purchase_invoices(supplier_id);

-- Index for inventory batch -> purchase invoice link
CREATE INDEX IF NOT EXISTS idx_batches_purchase_invoice_id ON inventory_batches(purchase_invoice_id);

-- Index for batch remaining units (speeds up stock queries)
CREATE INDEX IF NOT EXISTS idx_batches_medicine_remaining_units ON inventory_batches(medicine_id, remaining_units) WHERE remaining_units > 0;
