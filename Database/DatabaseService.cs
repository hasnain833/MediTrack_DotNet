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
            // Connection pooling: reuse connections instead of creating new ones per request
            _connectionString =
                $"Host={dbConfig["Host"]};Port={dbConfig["Port"]};" +
                $"Database={dbConfig["Database"]};Username={dbConfig["User"]};Password={dbConfig["Password"]};" +
                "Pooling=true;MinPoolSize=1;MaxPoolSize=10;Connection Lifetime=300;Command Timeout=30;";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            try
            {
                using var connection = new NpgsqlConnection(_connectionString);
                connection.Open();

                const string schema = @"
                    CREATE TABLE IF NOT EXISTS users (
                        id          SERIAL PRIMARY KEY,
                        username    VARCHAR(50) NOT NULL UNIQUE,
                        password    TEXT NOT NULL,
                        full_name   TEXT NOT NULL,
                        role        VARCHAR(20) NOT NULL DEFAULT 'Admin',
                        status      VARCHAR(20) NOT NULL DEFAULT 'Active',
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
                        id                SERIAL PRIMARY KEY,
                        medicine_id       INTEGER NOT NULL REFERENCES medicines(id) ON DELETE CASCADE,
                        supplier_id       INTEGER NOT NULL REFERENCES suppliers(id) ON DELETE RESTRICT,
                        batch_number      TEXT NOT NULL,
                        purchase_price    DECIMAL NOT NULL DEFAULT 0,
                        selling_price     DECIMAL NOT NULL DEFAULT 0,
                        stock_qty         INTEGER NOT NULL DEFAULT 0,
                        manufacture_date  DATE,
                        expiry_date       DATE NOT NULL,
                        created_at        TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
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
                    CREATE INDEX IF NOT EXISTS idx_batches_stock_positive
                        ON inventory_batches(stock_qty) WHERE stock_qty > 0;
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
                ";

                using (var command = new NpgsqlCommand(schema, connection))
                {
                    command.ExecuteNonQuery();
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
                        -- ── Multi-unit medicine support (Phase 1) ──
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='base_unit') THEN
                            ALTER TABLE medicines ADD COLUMN base_unit TEXT NOT NULL DEFAULT 'unit';
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='strip_size') THEN
                            ALTER TABLE medicines ADD COLUMN strip_size INTEGER;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='medicines' AND column_name='box_size') THEN
                            ALTER TABLE medicines ADD COLUMN box_size INTEGER;
                        END IF;
                        -- Track what unit was sold and how many base units were deducted
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='sold_unit') THEN
                            ALTER TABLE sale_items ADD COLUMN sold_unit TEXT;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name='sale_items' AND column_name='base_qty_deducted') THEN
                            ALTER TABLE sale_items ADD COLUMN base_qty_deducted INTEGER;
                        END IF;
                    END $$;";
                
                using (var migCmd = new NpgsqlCommand(migrationQuery, connection))
                {
                    migCmd.ExecuteNonQuery();
                }

                // Ensure settings table exists
                const string checkSettingsTableSql = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'settings')";
                using (var checkCmd = new NpgsqlCommand(checkSettingsTableSql, connection))
                {
                    if (!(bool)(checkCmd.ExecuteScalar() ?? false))
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
                        createCmd.ExecuteNonQuery();
                    }
                }

                // Ensure admin exists with new credentials
                using (var checkCmd = new NpgsqlCommand("SELECT id FROM users WHERE LOWER(username) = 'admin' LIMIT 1", connection))
                {
                    var userId = checkCmd.ExecuteScalar();
                    var hashedPassword = BCrypt.Net.BCrypt.HashPassword("@dmin8787");
                    
                    if (userId == null)
                    {
                        const string insertQuery = @"
                            INSERT INTO users (username, password, full_name, role, status)
                            VALUES ('Admin', @password, 'Administrator', 'Admin', 'Active')";
                        using var insertCmd = new NpgsqlCommand(insertQuery, connection);
                        insertCmd.Parameters.AddWithValue("@password", hashedPassword);
                        insertCmd.ExecuteNonQuery();
                    }
                    else
                    {
                        const string updateQuery = @"
                           UPDATE users SET username = 'Admin', password = @password WHERE id = @id";
                        using var updateCmd = new NpgsqlCommand(updateQuery, connection);
                        updateCmd.Parameters.AddWithValue("@password", hashedPassword);
                        updateCmd.Parameters.AddWithValue("@id", userId);
                        updateCmd.ExecuteNonQuery();
                    }
                }

                // Insert Sample Data if empty
                using (var checkDataCmd = new NpgsqlCommand("SELECT COUNT(*) FROM medicines", connection))
                {
                    if (Convert.ToInt64(checkDataCmd.ExecuteScalar()) == 0)
                    {
                        const string sampleDataText = @"
                            INSERT INTO categories (name) VALUES ('Pain Killer'), ('Antibiotic'), ('Cough Syrup');
                            INSERT INTO manufacturers (name) VALUES ('GSK'), ('Abbott'), ('Pfizer');
                            INSERT INTO suppliers (name, phone, address) VALUES ('ABC Pharma', '0300-1234567', 'Phase 6, Hayatabad, Peshawar');
                            
                            INSERT INTO medicines (name, generic_name, category_id, manufacturer_id, dosage_form, strength, barcode)
                            VALUES ('Panadol', 'Paracetamol', 1, 1, 'Tablet', '500mg', '625100123456');
                            
                            INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, manufacture_date, expiry_date)
                            VALUES (1, 1, 'PK1023', 1.5, 2.0, 500, '2024-01-01', '2027-05-01');
                        ";
                        using var insertDataCmd = new NpgsqlCommand(sampleDataText, connection);
                        insertDataCmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Database initialization failed", ex);
            }
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
