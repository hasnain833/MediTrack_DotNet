-- Migration: 20261004_001_drop_customer_ledger.sql
-- Udhaar (customer credit) was dropped as a feature; the supplier ledger stays.
DROP TABLE IF EXISTS customer_ledger;
