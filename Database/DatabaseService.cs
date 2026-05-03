using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DChemist.Utils;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;

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

                await EnsureBaseSchemaAsync(connection);
                await ApplyVersionedMigrationsAsync(connection);
                await EnsureSettingsTableAsync(connection);
                await EnsureAdminUserAsync(connection);
                await SeedSampleDataAsync(connection);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Database initialization failed", ex);
            }
        }

        private static async Task EnsureBaseSchemaAsync(NpgsqlConnection connection)
        {
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
                    units_per_pack  INTEGER NOT NULL DEFAULT 1,
                    packets_per_box INTEGER NOT NULL DEFAULT 1,
                    default_entry_mode TEXT NOT NULL DEFAULT 'Tablet',
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
                    fbr_reported      BOOLEAN NOT NULL DEFAULT FALSE,
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
                    quantity      INTEGER NOT NULL DEFAULT 0,
                    unit_price    DECIMAL NOT NULL,
                    subtotal      DECIMAL NOT NULL,
                    returned_qty  INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_medicines_barcode ON medicines(barcode);
                CREATE INDEX IF NOT EXISTS idx_batches_expiry ON inventory_batches(expiry_date);
                CREATE INDEX IF NOT EXISTS idx_medicines_name_lower ON medicines(lower(name));
                CREATE INDEX IF NOT EXISTS idx_medicines_generic_lower ON medicines(lower(generic_name));
                CREATE INDEX IF NOT EXISTS idx_batches_medicine_id ON inventory_batches(medicine_id);
                CREATE INDEX IF NOT EXISTS idx_sales_date_desc ON sales(sale_date DESC);
                CREATE INDEX IF NOT EXISTS idx_sale_items_sale_id ON sale_items(sale_id);

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

            using var command = new NpgsqlCommand(schema, connection);
            await command.ExecuteNonQueryAsync();

            // Ensure columns exist for existing databases
            const string migrationsSql = @"
                DO $$ BEGIN
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
            ";
            using var migrateCmd = new NpgsqlCommand(migrationsSql, connection);
            await migrateCmd.ExecuteNonQueryAsync();
        }

        private static async Task EnsureSettingsTableAsync(NpgsqlConnection connection)
        {
            const string checkSettingsTableSql = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'settings')";
            using var checkCmd = new NpgsqlCommand(checkSettingsTableSql, connection);
            if ((bool)(await checkCmd.ExecuteScalarAsync() ?? false)) return;

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

        private static async Task EnsureAdminUserAsync(NpgsqlConnection connection)
        {
            using var checkCmd = new NpgsqlCommand("SELECT id FROM users WHERE LOWER(username) = 'admin' LIMIT 1", connection);
            if (await checkCmd.ExecuteScalarAsync() != null) return;

            var hashedPassword = BCrypt.Net.BCrypt.HashPassword("@dmin8787");
            const string insertQuery = @"
                INSERT INTO users (username, password, full_name, role, status, must_change_password)
                VALUES ('Admin', @password, 'Administrator', 'Admin', 'Active', TRUE)";
            using var insertCmd = new NpgsqlCommand(insertQuery, connection);
            insertCmd.Parameters.AddWithValue("@password", hashedPassword);
            await insertCmd.ExecuteNonQueryAsync();
            AppLogger.LogInfo("Default Admin user created (password change required).");
        }

        private static async Task SeedSampleDataAsync(NpgsqlConnection connection)
        {
            using var checkDataCmd = new NpgsqlCommand("SELECT COUNT(*) FROM medicines", connection);
            if (Convert.ToInt64(await checkDataCmd.ExecuteScalarAsync()) != 0) return;

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

        private static async Task ApplyVersionedMigrationsAsync(NpgsqlConnection connection)
        {
            const string ensureMigrationsTableSql = @"
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    id SERIAL PRIMARY KEY,
                    filename TEXT NOT NULL UNIQUE,
                    applied_at TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                );
            ";
            await connection.ExecuteAsync(ensureMigrationsTableSql);

            var migrationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Database", "Migrations");
            if (!Directory.Exists(migrationsDir))
            {
                migrationsDir = Path.Combine(Directory.GetCurrentDirectory(), "Database", "Migrations");
            }
            if (!Directory.Exists(migrationsDir))
            {
                AppLogger.LogWarning("Database migrations folder not found. Skipping versioned migrations.");
                return;
            }

            var migrationFiles = Directory.GetFiles(migrationsDir, "*.sql")
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var file in migrationFiles)
            {
                var fileName = Path.GetFileName(file);
                var exists = await connection.ExecuteScalarAsync<int?>(
                    "SELECT 1 FROM schema_migrations WHERE filename = @fileName LIMIT 1",
                    new { fileName });

                if (exists.HasValue) continue;

                var sql = await File.ReadAllTextAsync(file);
                using var tx = await connection.BeginTransactionAsync();
                try
                {
                    await connection.ExecuteAsync(sql, transaction: tx);
                    await connection.ExecuteAsync(
                        "INSERT INTO schema_migrations (filename) VALUES (@fileName)",
                        new { fileName },
                        tx);
                    await tx.CommitAsync();
                    AppLogger.LogInfo($"Applied migration: {fileName}");
                }
                catch
                {
                    await tx.RollbackAsync();
                    throw;
                }
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

        private static async Task PrepareConnectionAsync(NpgsqlConnection connection)
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
            {
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<T>> FetchAllAsync<T>(string query, Func<NpgsqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            var results = new List<T>();
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new NpgsqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
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
            {
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            }
            using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
                return map(reader);
            return default;
        }
    }
}
