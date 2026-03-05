using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using Microsoft.Data.Sqlite;

namespace MediTrack.Repositories
{
    public class CustomerRepository
    {
        private readonly DatabaseService _db;

        public CustomerRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<Customer?> FindOrCreateAsync(string name, string? phone)
        {
            const string findQuery = "SELECT * FROM customers WHERE customer_name = @name AND phone = @phone LIMIT 1";
            var parameters = new Dictionary<string, object>
            {
                { "@name", name },
                { "@phone", phone ?? (object)DBNull.Value }
            };

            var customer = await _db.FetchOneAsync(findQuery, MapCustomer, parameters);
            if (customer != null) return customer;

            // SQLite uses last_insert_rowid() instead of LAST_INSERT_ID()
            const string insertQuery = "INSERT INTO customers (customer_name, phone) VALUES (@name, @phone); SELECT last_insert_rowid();";
            using var connection = _db.GetConnection();
            await connection.OpenAsync();
            using var command = new SqliteCommand(insertQuery, connection);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@phone", phone ?? (object)DBNull.Value);

            int id = Convert.ToInt32(await command.ExecuteScalarAsync());
            return new Customer { Id = id, CustomerName = name, Phone = phone };
        }

        private static Customer MapCustomer(SqliteDataReader reader)
        {
            return new Customer
            {
                Id           = Convert.ToInt32(reader["id"]),
                CustomerName = reader["customer_name"].ToString() ?? string.Empty,
                Phone        = reader["phone"] == DBNull.Value ? null : reader["phone"].ToString()
            };
        }
    }
}
