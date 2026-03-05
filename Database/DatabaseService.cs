using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace MediTrack.Database
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string dbFolder = Path.Combine(appDataPath, "MediTrack");
            
            if (!Directory.Exists(dbFolder))
            {
                Directory.CreateDirectory(dbFolder);
            }

            string dbPath = Path.Combine(dbFolder, "medical_store.db");
            _connectionString = $"Data Source={dbPath};Mode=ReadWriteCreate;Cache=Shared;";
            
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            const string schema = @"
                PRAGMA foreign_keys = ON;
                PRAGMA busy_timeout = 5000;

                CREATE TABLE IF NOT EXISTS users (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    username    TEXT NOT NULL UNIQUE,
                    password    TEXT NOT NULL,
                    full_name   TEXT NOT NULL,
                    role        TEXT NOT NULL DEFAULT 'Cashier',
                    status      TEXT NOT NULL DEFAULT 'Active',
                    created_at  TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
                );

                CREATE TABLE IF NOT EXISTS inventory (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    medicine_name   TEXT NOT NULL,
                    category        TEXT NOT NULL DEFAULT '',
                    price           REAL NOT NULL DEFAULT 0,
                    stock_qty       INTEGER NOT NULL DEFAULT 0,
                    expiry_date     TEXT NOT NULL,
                    supplier        TEXT NOT NULL DEFAULT '',
                    created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
                );

                CREATE TABLE IF NOT EXISTS customers (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    customer_name   TEXT NOT NULL,
                    phone           TEXT,
                    email           TEXT,
                    created_at      TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now'))
                );

                CREATE TABLE IF NOT EXISTS sales (
                    id                INTEGER PRIMARY KEY AUTOINCREMENT,
                    bill_no           TEXT NOT NULL UNIQUE,
                    user_id           INTEGER NOT NULL,
                    customer_id       INTEGER,
                    total_amount      REAL NOT NULL DEFAULT 0,
                    tax_amount        REAL NOT NULL DEFAULT 0,
                    discount_amount   REAL NOT NULL DEFAULT 0,
                    grand_total       REAL NOT NULL DEFAULT 0,
                    sale_date         TEXT NOT NULL DEFAULT (strftime('%Y-%m-%d %H:%M:%S', 'now')),
                    FOREIGN KEY (user_id)     REFERENCES users(id),
                    FOREIGN KEY (customer_id) REFERENCES customers(id)
                );

                CREATE TABLE IF NOT EXISTS sale_items (
                    id            INTEGER PRIMARY KEY AUTOINCREMENT,
                    sale_id       INTEGER NOT NULL,
                    inventory_id  INTEGER NOT NULL,
                    quantity      INTEGER NOT NULL DEFAULT 1,
                    unit_price    REAL NOT NULL,
                    subtotal      REAL NOT NULL,
                    FOREIGN KEY (sale_id)      REFERENCES sales(id) ON DELETE CASCADE,
                    FOREIGN KEY (inventory_id) REFERENCES inventory(id)
                );
            ";

            using (var command = new SqliteCommand(schema, connection))
            {
                command.ExecuteNonQuery();
            }

            // Migration: Add status column if it doesn't exist
            try
            {
                using var checkStatusCmd = new SqliteCommand("SELECT status FROM users LIMIT 1", connection);
                checkStatusCmd.ExecuteNonQuery();
            }
            catch
            {
                // Column missing, add it
                using var alterCmd = new SqliteCommand("ALTER TABLE users ADD COLUMN status TEXT NOT NULL DEFAULT 'Active'", connection);
                alterCmd.ExecuteNonQuery();
            }

            // Check if default admin exists
            using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM users WHERE username = 'admin'", connection))
            {
                var result = checkCmd.ExecuteScalar();
                if (result == null || Convert.ToInt64(result) == 0)
                {
                    // admin / admin123
                    const string insertQuery = @"
                        INSERT INTO users (username, password, full_name, role, status)
                        VALUES ('admin', '$2a$11$s.PnrFnkBJfz7HDCA3ZMB.0gTbSAe4f2blKoW5y3wGEwJXqSi/P/2', 'Administrator', 'Admin', 'Active')";
                    using var insertCmd = new SqliteCommand(insertQuery, connection);
                    insertCmd.ExecuteNonQuery();
                }
            }

            using (var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM users WHERE username = 'cashier'", connection))
            {
                var result = checkCmd.ExecuteScalar();
                if (result == null || Convert.ToInt64(result) == 0)
                {
                    // cashier / cashier123
                    const string insertQuery = @"
                        INSERT INTO users (username, password, full_name, role, status)
                        VALUES ('cashier', '$2a$11$qR7iWcK.q1.B9F0E1G.H.O5B/rB3T.dE7n8m9p0q1r2s3t4u5v6w.', 'Test Cashier', 'Cashier', 'Active')";
                    using var insertCmd = new SqliteCommand(insertQuery, connection);
                    insertCmd.ExecuteNonQuery();
                }
            }
        }

        public SqliteConnection GetConnection()
        {
            return new SqliteConnection(_connectionString);
        }

        private async Task PrepareConnectionAsync(SqliteConnection connection)
        {
            if (connection.State != ConnectionState.Open)
                await connection.OpenAsync();
            
            using var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;", connection);
            await pragmaCmd.ExecuteNonQueryAsync();
        }

        public async Task ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new SqliteCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<T>> FetchAllAsync<T>(string query, Func<SqliteDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            var results = new List<T>();
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new SqliteCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            using var reader = (SqliteDataReader)await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                results.Add(map(reader));
            return results;
        }

        public async Task<T?> FetchOneAsync<T>(string query, Func<SqliteDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await PrepareConnectionAsync(connection);
            using var command = new SqliteCommand(query, connection);
            if (parameters != null)
                foreach (var param in parameters)
                    command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
            using var reader = (SqliteDataReader)await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
                return map(reader);
            return default;
        }
    }
}
