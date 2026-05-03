-- Migration to add Box/Packet configuration to Medicine Master Data
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='units_per_pack') THEN
        ALTER TABLE medicines ADD COLUMN units_per_pack INTEGER NOT NULL DEFAULT 1;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='packets_per_box') THEN
        ALTER TABLE medicines ADD COLUMN packets_per_box INTEGER NOT NULL DEFAULT 1;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='default_entry_mode') THEN
        ALTER TABLE medicines ADD COLUMN default_entry_mode TEXT NOT NULL DEFAULT 'Tablet';
    END IF;
END $$;
