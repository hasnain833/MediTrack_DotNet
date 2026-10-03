-- Migration: 20261005_001_drop_supplier_ledger.sql
-- The supplier ledger was dropped as a feature (not needed). Bonus / discount / Net columns stay.
DROP TABLE IF EXISTS supplier_ledger;
