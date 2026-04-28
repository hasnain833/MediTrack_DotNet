DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='fbr_reported') THEN
        ALTER TABLE sales ADD COLUMN fbr_reported BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='fbr_invoice_no') THEN
        ALTER TABLE sales ADD COLUMN fbr_invoice_no TEXT UNIQUE;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='fbr_response') THEN
        ALTER TABLE sales ADD COLUMN fbr_response TEXT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='status') THEN
        ALTER TABLE sales ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Completed';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='customers' AND column_name='phone') THEN
        ALTER TABLE customers ADD COLUMN phone TEXT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='customers' AND column_name='email') THEN
        ALTER TABLE customers ADD COLUMN email TEXT;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='quantity') THEN
        ALTER TABLE sale_items ADD COLUMN quantity INTEGER NOT NULL DEFAULT 0;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='returned_qty') THEN
        ALTER TABLE sale_items ADD COLUMN returned_qty INTEGER NOT NULL DEFAULT 0;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='gst_percent') THEN
        ALTER TABLE medicines ADD COLUMN gst_percent NUMERIC NOT NULL DEFAULT 0;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='base_unit') THEN
        ALTER TABLE medicines DROP COLUMN base_unit;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='strip_size') THEN
        ALTER TABLE medicines DROP COLUMN strip_size;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='box_size') THEN
        ALTER TABLE medicines DROP COLUMN box_size;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='batch_number') THEN
        ALTER TABLE inventory_batches RENAME COLUMN batch_number TO batch_no;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='stock_qty') THEN
        ALTER TABLE inventory_batches RENAME COLUMN stock_qty TO remaining_units;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='purchase_price') THEN
        ALTER TABLE inventory_batches RENAME COLUMN purchase_price TO unit_cost;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='quantity_units') THEN
        ALTER TABLE inventory_batches ADD COLUMN quantity_units INTEGER NOT NULL DEFAULT 0;
        EXECUTE 'UPDATE inventory_batches SET quantity_units = remaining_units WHERE quantity_units = 0';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='purchase_total_price') THEN
        ALTER TABLE inventory_batches ADD COLUMN purchase_total_price DECIMAL NOT NULL DEFAULT 0;
        EXECUTE 'UPDATE inventory_batches SET purchase_total_price = unit_cost * quantity_units WHERE purchase_total_price = 0';
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='sold_unit') THEN
        ALTER TABLE sale_items DROP COLUMN sold_unit;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='base_qty_deducted') THEN
        ALTER TABLE sale_items DROP COLUMN base_qty_deducted;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='invoice_no') THEN
        ALTER TABLE inventory_batches ADD COLUMN invoice_no TEXT;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='invoice_date') THEN
        ALTER TABLE inventory_batches ADD COLUMN invoice_date DATE;
    END IF;

    ALTER TABLE inventory_batches ALTER COLUMN supplier_id DROP NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='must_change_password') THEN
        ALTER TABLE users ADD COLUMN must_change_password BOOLEAN NOT NULL DEFAULT FALSE;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='payment_status') THEN
        ALTER TABLE inventory_batches ADD COLUMN payment_status TEXT NOT NULL DEFAULT 'Cash';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='entry_mode') THEN
        ALTER TABLE inventory_batches ADD COLUMN entry_mode TEXT NOT NULL DEFAULT 'Tablet';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='units_per_pack') THEN
        ALTER TABLE inventory_batches ADD COLUMN units_per_pack INTEGER NOT NULL DEFAULT 1;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='pack_quantity') THEN
        ALTER TABLE inventory_batches ADD COLUMN pack_quantity INTEGER NOT NULL DEFAULT 0;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS idx_medicines_barcode ON medicines(barcode);
CREATE INDEX IF NOT EXISTS idx_medicines_name ON medicines(name);
CREATE INDEX IF NOT EXISTS idx_sales_bill_no ON sales(bill_no);
CREATE INDEX IF NOT EXISTS idx_sales_sale_date ON sales(sale_date);
CREATE INDEX IF NOT EXISTS idx_batches_medicine_id ON inventory_batches(medicine_id);
CREATE INDEX IF NOT EXISTS idx_batches_expiry_date ON inventory_batches(expiry_date);
CREATE INDEX IF NOT EXISTS idx_batches_stock_positive ON inventory_batches(remaining_units) WHERE remaining_units > 0;

CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX IF NOT EXISTS idx_medicines_name_trgm ON medicines USING GIST (name gist_trgm_ops);
CREATE INDEX IF NOT EXISTS idx_medicines_generic_trgm ON medicines USING GIST (generic_name gist_trgm_ops);
