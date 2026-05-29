-- Migration: 20260529_001_batch_history
-- Purpose: Track historical batch numbers per medicine for drug inspector traceability.
-- When a batch_no is changed/replaced, the old batch_no is preserved here
-- along with the invoice details so it can always be traced back.

CREATE TABLE IF NOT EXISTS batch_history (
    id                  SERIAL PRIMARY KEY,
    medicine_id         INTEGER NOT NULL REFERENCES medicines(id) ON DELETE CASCADE,
    medicine_name       TEXT    NOT NULL,
    old_batch_no        TEXT    NOT NULL,
    new_batch_no        TEXT,
    supplier_id         INTEGER REFERENCES suppliers(id) ON DELETE SET NULL,
    supplier_name       TEXT,
    invoice_no          TEXT    NOT NULL,
    invoice_date        DATE    NOT NULL,
    quantity_units      INTEGER NOT NULL DEFAULT 0,
    purchase_total_price DECIMAL NOT NULL DEFAULT 0,
    unit_cost           DECIMAL NOT NULL DEFAULT 0,
    inventory_batch_id  INTEGER REFERENCES inventory_batches(id) ON DELETE SET NULL,
    change_reason       TEXT    NOT NULL DEFAULT 'Batch number updated on new purchase',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_batch_history_medicine_id   ON batch_history(medicine_id);
CREATE INDEX IF NOT EXISTS idx_batch_history_old_batch_no  ON batch_history(lower(old_batch_no));
CREATE INDEX IF NOT EXISTS idx_batch_history_invoice_no    ON batch_history(lower(invoice_no));
CREATE INDEX IF NOT EXISTS idx_batch_history_created_at    ON batch_history(created_at DESC);
