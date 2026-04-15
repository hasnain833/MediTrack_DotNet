using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DChemist.Utils;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace DChemist.Database
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(IConfiguration configuration)
        {
            var dbConfig = configuration.GetSection("Database");
            
            var builder = new NpgsqlConnectionStringBuilder
            {
                Host = dbConfig["Host"],
                Port = int.Parse(dbConfig["Port"] ?? "5432"),
                Database = dbConfig["Database"],
                Username = dbConfig["User"],
                Password = dbConfig["Password"],
                Pooling = true
            };
            
            _connectionString = builder.ToString();
        }

        public async Task InitializeAsync()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                await connection.OpenAsync();

                const string schema = @"
                    CREATE TABLE IF NOT EXISTS users (
                        id          SERIAL PRIMARY KEY,
                        username    VARCHAR(50) NOT NULL UNIQUE,
                        password    TEXT NOT NULL,
                        full_name   TEXT NOT NULL,
                        role        VARCHAR(20) NOT NULL DEFAULT 'Admin',
                        status      VARCHAR(20) NOT NULL DEFAULT 'Active',
                        must_change_password BOOLEAN NOT NULL DEFAULT FALSE,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS categories (
                        id          SERIAL PRIMARY KEY,
                        name        TEXT NOT NULL UNIQUE,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS manufacturers (
                        id          SERIAL PRIMARY KEY,
                        name        TEXT NOT NULL UNIQUE,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS suppliers (
                        id          SERIAL PRIMARY KEY,
                        name        TEXT NOT NULL,
                        phone       TEXT,
                        address     TEXT,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS medicines (
                        id              SERIAL PRIMARY KEY,
                        name            TEXT NOT NULL,
                        generic_name    TEXT,
                        category_id     INTEGER REFERENCES categories(id) ON DELETE SET NULL,
                        manufacturer_id INTEGER REFERENCES manufacturers(id) ON DELETE SET NULL,
                        dosage_form     TEXT,
                        strength        TEXT,
                        barcode         TEXT UNIQUE,
                        created_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS inventory_batches (
                        id                    SERIAL PRIMARY KEY,
                        medicine_id           INTEGER NOT NULL REFERENCES medicines(id) ON DELETE CASCADE,
                        supplier_id           INTEGER REFERENCES suppliers(id) ON DELETE RESTRICT,
                        batch_no              TEXT NOT NULL,
                        quantity_units        INTEGER NOT NULL DEFAULT 0,
                        purchase_total_price  DECIMAL NOT NULL DEFAULT 0,
                        unit_cost             DECIMAL NOT NULL DEFAULT 0,
                        selling_price         DECIMAL NOT NULL DEFAULT 0,
                        remaining_units       INTEGER NOT NULL DEFAULT 0,
                        manufacture_date      DATE,
                        expiry_date           DATE NOT NULL,
                        created_at            TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS customers (
                        id              SERIAL PRIMARY KEY,
                        customer_name   TEXT NOT NULL,
                        phone           TEXT,
                        email           TEXT,
                        created_at      TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS sales (
                        id                SERIAL PRIMARY KEY,
                        bill_no           TEXT NOT NULL UNIQUE,
                        user_id           INTEGER NOT NULL REFERENCES users(id),
                        customer_id       INTEGER REFERENCES customers(id),
                        total_amount      DECIMAL NOT NULL DEFAULT 0,
                        tax_amount        DECIMAL NOT NULL DEFAULT 0,
                        discount_amount   DECIMAL NOT NULL DEFAULT 0,
                        grand_total       DECIMAL NOT NULL DEFAULT 0,
                        fbr_invoice_no    TEXT UNIQUE,
                        fbr_response      TEXT,
                        status            VARCHAR(20) NOT NULL DEFAULT 'Completed',
                        sale_date         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS sale_items (
                        id            SERIAL PRIMARY KEY,
                        sale_id       INTEGER NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
                        medicine_id   INTEGER REFERENCES medicines(id),
                        batch_id      INTEGER REFERENCES inventory_batches(id),
                        unit_price    DECIMAL NOT NULL,
                        subtotal      DECIMAL NOT NULL,
                        returned_qty  INTEGER NOT NULL DEFAULT 0
                    );

                    CREATE INDEX IF NOT EXISTS idx_medicines_barcode ON medicines(barcode);
                    CREATE INDEX IF NOT EXISTS idx_batches_expiry ON inventory_batches(expiry_date);

                    -- Performance indexes added for search and JOIN efficiency
                    CREATE INDEX IF NOT EXISTS idx_medicines_name_lower
                        ON medicines(lower(name));
                    CREATE INDEX IF NOT EXISTS idx_medicines_generic_lower
                        ON medicines(lower(generic_name));
                    CREATE INDEX IF NOT EXISTS idx_batches_medicine_id
                        ON inventory_batches(medicine_id);
                    CREATE INDEX IF NOT EXISTS idx_sales_date_desc
                        ON sales(sale_date DESC);
                    CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id
                        ON sale_items(sale_id);

                    CREATE TABLE IF NOT EXISTS audit_logs (
                        id          SERIAL PRIMARY KEY,
                        user_id     INTEGER REFERENCES users(id) ON DELETE SET NULL,
                        action      VARCHAR(50) NOT NULL,
                        details     TEXT,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IF NOT EXISTS idx_audit_logs_created_at ON audit_logs(created_at DESC);

                    CREATE TABLE IF NOT EXISTS error_logs (
                        id          SERIAL PRIMARY KEY,
                        message     TEXT NOT NULL,
                        stack_trace TEXT,
                        source      TEXT,
                        created_at  TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    CREATE INDEX IF NOT EXISTS idx_error_logs_created_at ON error_logs(created_at DESC);
                ";

                using (var command = new NpgsqlCommand(schema, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }

                // ── MIGRATIONS: Add columns that might be missing from older installs ──
                const string migrationQuery = @"
                    -- Add FBR columns to sales if missing
                    DO $$ 
                    BEGIN 
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='fbr_invoice_no') THEN
                            ALTER TABLE sales ADD COLUMN fbr_invoice_no TEXT UNIQUE;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='fbr_response') THEN
                            ALTER TABLE sales ADD COLUMN fbr_response TEXT;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sales' AND column_name='status') THEN
                            ALTER TABLE sales ADD COLUMN status VARCHAR(20) NOT NULL DEFAULT 'Completed';
                        END IF;
                        -- Ensure customer columns exist (for edge cases)
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='customers' AND column_name='phone') THEN
                            ALTER TABLE customers ADD COLUMN phone TEXT;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='customers' AND column_name='email') THEN
                            ALTER TABLE customers ADD COLUMN email TEXT;
                        END IF;
                        -- Add returned_qty to sale_items if missing
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='returned_qty') THEN
                            ALTER TABLE sale_items ADD COLUMN returned_qty INTEGER NOT NULL DEFAULT 0;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='gst_percent') THEN
                            ALTER TABLE medicines ADD COLUMN gst_percent NUMERIC NOT NULL DEFAULT 0;
                        END IF;
                        
                        -- ── Multi-unit medicine support REMOVAL ──
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='base_unit') THEN
                            ALTER TABLE medicines DROP COLUMN base_unit;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='strip_size') THEN
                            ALTER TABLE medicines DROP COLUMN strip_size;
                        END IF;
                        IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='box_size') THEN
                            ALTER TABLE medicines DROP COLUMN box_size;
                        END IF;
                        
                        -- Inventory batch simplifications
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
                            -- Fallback logic for old systems being upgraded to map stock_qty
                            EXECUTE 'UPDATE inventory_batches SET quantity_units = remaining_units WHERE quantity_units = 0';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='purchase_total_price') THEN
                            ALTER TABLE inventory_batches ADD COLUMN purchase_total_price DECIMAL NOT NULL DEFAULT 0;
                            -- Migrate existing purchase_price assumptions
                            EXECUTE 'UPDATE inventory_batches SET purchase_total_price = unit_cost * quantity_units WHERE purchase_total_price = 0';
                        END IF;
                        
                        -- Track what unit was sold REMOVAL
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
                        -- Make supplier_id nullable to allow initial registration without a supplier
                        ALTER TABLE inventory_batches ALTER COLUMN supplier_id DROP NOT NULL;
                        
                        -- Add must_change_password to users
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='users' AND column_name='must_change_password') THEN
                            ALTER TABLE users ADD COLUMN must_change_password BOOLEAN NOT NULL DEFAULT FALSE;
                        END IF;
                        
                        -- Phase 2: Payment status for batches
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='payment_status') THEN
                            ALTER TABLE inventory_batches ADD COLUMN payment_status TEXT NOT NULL DEFAULT 'Cash';
                        END IF;

                        -- Phase 4: Performance Indexes (Section 9)
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_medicines_barcode') THEN
                            CREATE INDEX idx_medicines_barcode ON medicines(barcode);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_medicines_name') THEN
                            CREATE INDEX idx_medicines_name ON medicines(name);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_sales_bill_no') THEN
                            CREATE INDEX idx_sales_bill_no ON sales(bill_no);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_sales_sale_date') THEN
                            CREATE INDEX idx_sales_sale_date ON sales(sale_date);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_batches_medicine_id') THEN
                            CREATE INDEX idx_batches_medicine_id ON inventory_batches(medicine_id);
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE indexname = 'idx_batches_expiry_date') THEN
                            CREATE INDEX idx_batches_expiry_date ON inventory_batches(expiry_date);
                        END IF;

                        -- New Quantity Tracking Columns
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='entry_mode') THEN
                            ALTER TABLE inventory_batches ADD COLUMN entry_mode TEXT NOT NULL DEFAULT 'Tablet';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='units_per_pack') THEN
                            ALTER TABLE inventory_batches ADD COLUMN units_per_pack INTEGER NOT NULL DEFAULT 1;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='inventory_batches' AND column_name='pack_quantity') THEN
                            ALTER TABLE inventory_batches ADD COLUMN pack_quantity INTEGER NOT NULL DEFAULT 0;
                        END IF;
                    END $$;";
                
                using (var migCmd = new NpgsqlCommand(migrationQuery, connection))
                {
                    await migCmd.ExecuteNonQueryAsync();
                }
                const string postMigrationIndexes = @"
                    CREATE INDEX IF NOT EXISTS idx_batches_stock_positive
                        ON inventory_batches(remaining_units) WHERE remaining_units > 0;
                ";
                using (var idxCmd = new NpgsqlCommand(postMigrationIndexes, connection))
                {
                    await idxCmd.ExecuteNonQueryAsync();
                }

                // Phase 2: Fuzzy Search Extensions
                const string fuzzySearchSql = @"
                    CREATE EXTENSION IF NOT EXISTS pg_trgm;
                    CREATE INDEX IF NOT EXISTS idx_medicines_name_trgm ON medicines USING GIST (name gist_trgm_ops);
                    CREATE INDEX IF NOT EXISTS idx_medicines_generic_trgm ON medicines USING GIST (generic_name gist_trgm_ops);
                ";
                using (var fuzzyCmd = new NpgsqlCommand(fuzzySearchSql, connection))
                {
                    await fuzzyCmd.ExecuteNonQueryAsync();
                }

                // Ensure settings table exists
                const string checkSettingsTableSql = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'settings')";
                using (var checkCmd = new NpgsqlCommand(checkSettingsTableSql, connection))
                {
                    if (!(bool)(await checkCmd.ExecuteScalarAsync() ?? false))
                    {
                        const string createSettingsSql = @"
                            CREATE TABLE settings (
                                key TEXT PRIMARY KEY,
                                value TEXT
                            );
                            INSERT INTO settings (key, value) VALUES ('tax_rate', '0.0');
                            INSERT INTO settings (key, value) VALUES ('fbr_pos_id', 'DChemist-POS-001');
                            INSERT INTO settings (key, value) VALUES ('fbr_api_url', 'https://ims.fbr.gov.pk/api/v3/Post/PostInvoice');
                            INSERT INTO settings (key, value) VALUES ('fbr_is_live', 'false');
                            INSERT INTO settings (key, value) VALUES ('fbr_token', '');
                        ";
                        using var createCmd = new NpgsqlCommand(createSettingsSql, connection);
                        await createCmd.ExecuteNonQueryAsync();
                    }
                }

                // Ensure admin exists with default credentials ONLY if not present
                using (var checkCmd = new NpgsqlCommand("SELECT id FROM users WHERE LOWER(username) = 'admin' LIMIT 1", connection))
                {
                    if (await checkCmd.ExecuteScalarAsync() == null)
                    {
                        var hashedPassword = BCrypt.Net.BCrypt.HashPassword("@dmin8787");
                        const string insertQuery = @"
                            INSERT INTO users (username, password, full_name, role, status, must_change_password)
                            VALUES ('Admin', @password, 'Administrator', 'Admin', 'Active', TRUE)";
                        using var insertCmd = new NpgsqlCommand(insertQuery, connection);
                        insertCmd.Parameters.AddWithValue("@password", hashedPassword);
                        await insertCmd.ExecuteNonQueryAsync();
                        AppLogger.LogInfo("Default Admin user created (password change required).");
                    }
                }

                // Insert Sample Data if empty
                using (var checkDataCmd = new NpgsqlCommand("SELECT COUNT(*) FROM medicines", connection))
                {
                    if (Convert.ToInt64(await checkDataCmd.ExecuteScalarAsync()) == 0)
                    {
                        const string sampleDataText = @"
                            INSERT INTO categories (name) VALUES ('Pain Killer'), ('Antibiotic'), ('Cough Syrup');
                            INSERT INTO manufacturers (name) VALUES ('GSK'), ('Abbott'), ('Pfizer');
                            INSERT INTO suppliers (name, phone, address) VALUES ('ABC Pharma', '0300-1234567', 'Phase 6, Hayatabad, Peshawar');
                            
                            INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode)
                            VALUES ('Panadol', 'Paracetamol', 1, 1, 'Tablet', '500mg', '625100123456');
                            
                            INSERT INTO inventory_batches (medicine_id, supplier_id, batch_no, quantity_units, purchase_total_price, unit_cost, selling_price, remaining_units, manufacture_date, expiry_date)
                            VALUES (1, 1, 'PK1023', 500, 750, 1.5, 2.0, 500, '2024-01-01', '2027-05-01');
                        ";
                        using var insertDataCmd = new NpgsqlCommand(sampleDataText, connection);
                        await insertDataCmd.ExecuteNonQueryAsync();
                    }
                }
                // Database initialized successfully.
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Database initialization failed", ex);
            }
        }

        public async Task HardResetAsync(NpgsqlConnection connection)
        {
            const string truncateSql = @"
                TRUNCATE TABLE sale_items, sales, inventory_batches, medicines, 
                               manufacturers, categories, suppliers, customers, 
                               audit_logs, error_logs RESTART IDENTITY CASCADE;";
            
            using var cmd = new NpgsqlCommand(truncateSql, connection);
            await cmd.ExecuteNonQueryAsync();
            AppLogger.LogInfo("Database Hard Reset completed successfully.");
        }

        public NpgsqlConnection GetConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }

        private async Task PrepareConnectionAsync(NpgsqlConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
        }

        public async Task ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new NpgsqlCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<T>> FetchAllAsync<T>(string query, Func<NpgsqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            var results = new List<T>();
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new NpgsqlCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(map(reader));
            return results;
        }

        public async Task<T?> FetchOneAsync<T>(string query, Func<NpgsqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new NpgsqlCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
                return map(reader);
            return default;
        }
    }
}
