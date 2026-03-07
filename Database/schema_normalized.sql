-- MediTrack Normalized Database Schema
-- Designed for D. ChemistPharmacy Management System

PRAGMA foreign_keys = ON;

-- 1. Categories Table
CREATE TABLE IF NOT EXISTS categories (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT NOT NULL UNIQUE,
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
);

-- 2. Manufacturers Table
CREATE TABLE IF NOT EXISTS manufacturers (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT NOT NULL UNIQUE,
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
);

-- 3. Suppliers Table
CREATE TABLE IF NOT EXISTS suppliers (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    name        TEXT NOT NULL,
    phone       TEXT,
    address     TEXT,
    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
);

-- 4. Medicines Table (Master Data)
CREATE TABLE IF NOT EXISTS medicines (
    id              INTEGER PRIMARY KEY AUTOINCREMENT,
    name            TEXT NOT NULL,
    generic_name    TEXT,
    category_id     INTEGER,
    manufacturer_id INTEGER,
    dosage_form     TEXT, -- e.g., Tablet, Syrup
    strength        TEXT, -- e.g., 500mg
    barcode         TEXT UNIQUE,
    created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now')),
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL,
    FOREIGN KEY (manufacturer_id) REFERENCES manufacturers(id) ON DELETE SET NULL
);

-- 5. Inventory Batches Table (Transaction/Stock Details)
CREATE TABLE IF NOT EXISTS inventory_batches (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    medicine_id       INTEGER NOT NULL,
    supplier_id       INTEGER NOT NULL,
    batch_number      TEXT NOT NULL,
    purchase_price    REAL NOT NULL DEFAULT 0,
    selling_price     REAL NOT NULL DEFAULT 0,
    stock_qty         INTEGER NOT NULL DEFAULT 0,
    manufacture_date  TEXT, -- ISO format YYYY-MM-DD
    expiry_date       TEXT NOT NULL, -- ISO format YYYY-MM-DD
    created_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now')),
    FOREIGN KEY (medicine_id) REFERENCES medicines(id) ON DELETE CASCADE,
    FOREIGN KEY (supplier_id) REFERENCES suppliers(id) ON DELETE RESTRICT
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_medicines_barcode ON medicines(barcode);
CREATE INDEX IF NOT EXISTS idx_batches_expiry ON inventory_batches(expiry_date);
CREATE INDEX IF NOT EXISTS idx_batches_medicine ON inventory_batches(medicine_id);

-- Sample Data Insertion
-- Insert Categories
INSERT INTO categories (name) VALUES ('Pain Killer'), ('Antibiotic'), ('Cough Syrup');

-- Insert Manufacturers
INSERT INTO manufacturers (name) VALUES ('GSK'), ('Abbott'), ('Pfizer');

-- Insert Suppliers
INSERT INTO suppliers (name, phone, address) VALUES ('ABC Pharma', '0300-1234567', 'Phase 6, Hayatabad, Peshawar');

-- Insert Medicines
INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode)
VALUES ('Panadol', 'Paracetamol', 1, 1, 'Tablet', '500mg', '625100123456');

-- Insert Inventory Batches
INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, manufacture_date, expiry_date)
VALUES (1, 1, 'PK1023', 1.5, 2.0, 500, '2024-01-01', '2027-05-01');

-- Query to view detailed inventory
-- Features: Medicine Name | Category | Manufacturer | Batch | Stock | Expiry Date | Supplier
SELECT 
    m.name AS "Medicine Name",
    c.name AS "Category",
    man.name AS "Manufacturer",
    b.batch_number AS "Batch",
    b.stock_qty AS "Stock",
    b.expiry_date AS "Expiry Date",
    s.name AS "Supplier"
FROM medicines m
JOIN categories c ON m.category_id = c.id
JOIN manufacturers man ON m.manufacturer_id = man.id
JOIN inventory_batches b ON b.medicine_id = m.id
JOIN suppliers s ON b.supplier_id = s.id;
