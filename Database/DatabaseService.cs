using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace MediTrack.Database
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string server, string database, string user, string password)
        {
            _connectionString = $"Server={server};Database={database};Uid={user};Pwd={password};SslMode=None;";
        }

        public MySqlConnection GetConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        public async Task ExecuteNonQueryAsync(string query, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new MySqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<T>> FetchAllAsync<T>(string query, Func<MySqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            var results = new List<T>();
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new MySqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(map(reader));
            }
            return results;
        }

        public async Task<T?> FetchOneAsync<T>(string query, Func<MySqlDataReader, T> map, Dictionary<string, object>? parameters = null)
        {
            using var connection = GetConnection();
            await connection.OpenAsync();
            using var command = new MySqlCommand(query, connection);
            if (parameters != null)
            {
                foreach (var param in parameters)
                {
                    command.Parameters.AddWithValue(param.Key, param.Value);
                }
            }
            using var reader = (MySqlDataReader)await command.ExecuteReaderAsync(CommandBehavior.SingleRow);
            if (await reader.ReadAsync())
            {
                return map(reader);
            }
            return default;
        }
    }
}
