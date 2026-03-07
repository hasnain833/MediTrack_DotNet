using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace MediTrack.Database
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var dbConfig = configuration.GetSection("Database");
            _connectionString = $"Host={dbConfig["Host"]};Port={dbConfig["Port"]};Database={dbConfig["Database"]};Username={dbConfig["User"]};Password={dbConfig["Password"]};";
            
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
                        sale_date         TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );

                    CREATE TABLE IF NOT EXISTS sale_items (
                        id            SERIAL PRIMARY KEY,
                        sale_id       INTEGER NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
                        medicine_id   INTEGER REFERENCES medicines(id),
                        batch_id      INTEGER REFERENCES inventory_batches(id),
                        quantity      INTEGER NOT NULL DEFAULT 1,
                        unit_price    DECIMAL NOT NULL,
                        subtotal      DECIMAL NOT NULL
                    );

                    CREATE INDEX IF NOT EXISTS idx_medicines_barcode ON medicines(barcode);
                    CREATE INDEX IF NOT EXISTS idx_batches_expiry ON inventory_batches(expiry_date);
                ";

                using (var command = new NpgsqlCommand(schema, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Check if default admin exists
                using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM users WHERE username = 'admin'", connection))
                {
                    if (Convert.ToInt64(checkCmd.ExecuteScalar()) == 0)
                    {
                        const string insertQuery = @"
                            INSERT INTO users (username, password, full_name, role, status)
                            VALUES ('admin', '$2a$11$s.PnrFnkBJfz7HDCA3ZMB.0gTbSAe4f2blKoW5y3wGEwJXqSi/P/2', 'Administrator', 'Admin', 'Active')";
                        using var insertCmd = new NpgsqlCommand(insertQuery, connection);
                        insertCmd.ExecuteNonQuery();
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
                System.Diagnostics.Debug.WriteLine($"[DatabaseService] Initialization Error: {ex.Message}");
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
