-- Migration: 20261003_001_bonus_net_ledgers.sql
-- Bonus (free) quantity + distributor discount on purchases, "Net" medicines (no discount),
-- and supplier / customer ledgers (udhaar).

-- Net item: the amount actually paid differs from the supplier invoice price
-- (e.g. box Rs 200, invoice Rs 150, actually paid Rs 50). Cost/profit/ledger use the actual amount.
ALTER TABLE medicines ADD COLUMN IF NOT EXISTS is_net BOOLEAN NOT NULL DEFAULT FALSE;

-- Latest purchase terms for the batch row. quantity_units/remaining_units INCLUDE bonus units,
-- purchase_total_price is what was ACTUALLY paid (after discount / net deal), so unit_cost reflects it.
-- invoice_amount keeps the total as printed on the supplier invoice, for reference.
ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS bonus_units      INTEGER NOT NULL DEFAULT 0;
ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS discount_percent DECIMAL NOT NULL DEFAULT 0;
ALTER TABLE inventory_batches ADD COLUMN IF NOT EXISTS invoice_amount   DECIMAL NOT NULL DEFAULT 0;

-- Ledgers are their own tables (not derived from purchase_invoices/sales) because fully-sold
-- invoices are auto-deleted and sales can be voided; the money history must survive both.
-- amount > 0 increases the balance owed, amount < 0 reduces it.
--   supplier_ledger: balance = what the shop owes the supplier
--   customer_ledger: balance = what the customer owes the shop
CREATE TABLE IF NOT EXISTS supplier_ledger (
    id           SERIAL PRIMARY KEY,
    supplier_id  INTEGER NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
    entry_type   VARCHAR(20) NOT NULL,   -- Purchase, Payment, Opening, Adjustment
    reference    TEXT,                   -- invoice no
    amount       DECIMAL NOT NULL,
    notes        TEXT,
    user_id      INTEGER REFERENCES users(id),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_supplier_ledger_supplier ON supplier_ledger(supplier_id, created_at);

CREATE TABLE IF NOT EXISTS customer_ledger (
    id           SERIAL PRIMARY KEY,
    customer_id  INTEGER NOT NULL REFERENCES customers(id) ON DELETE RESTRICT,
    entry_type   VARCHAR(20) NOT NULL,   -- Sale, Payment, Return, Void, Opening, Adjustment
    reference    TEXT,                   -- bill no
    amount       DECIMAL NOT NULL,
    notes        TEXT,
    user_id      INTEGER REFERENCES users(id),
    created_at   TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);
CREATE INDEX IF NOT EXISTS idx_customer_ledger_customer ON customer_ledger(customer_id, created_at);
CREATE INDEX IF NOT EXISTS idx_customer_ledger_reference ON customer_ledger(reference);
