-- Migration: 20261008_001_bound_numeric_scale.sql
-- Every money/percent column was unbounded NUMERIC, so C# divisions (e.g. unit_cost = 280 / 3)
-- were stored with ~28 decimal places. Multiplying or summing those in SQL (dashboard profit,
-- daily sales) produced numerics with more than 28 significant digits, which System.Decimal
-- cannot hold -> OverflowException on read. Cap every unbounded numeric column at 4 decimals;
-- existing values are rounded and Postgres rounds all future writes.
DO $$
DECLARE c RECORD;
BEGIN
    FOR c IN
        SELECT table_name, column_name
        FROM information_schema.columns
        WHERE table_schema = 'public' AND data_type = 'numeric' AND numeric_precision IS NULL
    LOOP
        EXECUTE format('ALTER TABLE %I ALTER COLUMN %I TYPE NUMERIC(18,4)', c.table_name, c.column_name);
    END LOOP;
END $$;
