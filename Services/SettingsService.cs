using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using Npgsql;

namespace DChemist.Services
{
    public class SettingsService
    {
        private readonly DatabaseService _db;

        public SettingsService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<string> GetSettingAsync(string key, string defaultValue = "")
        {
            const string query = "SELECT value FROM settings WHERE key = @key";
            var result = await _db.FetchOneAsync(query, reader => reader.GetString(0), new Dictionary<string, object> { { "@key", key } });
            return result ?? defaultValue;
        }

        public async Task SaveSettingAsync(string key, string value)
        {
            const string query = @"
                INSERT INTO settings (key, value) VALUES (@key, @value)
                ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value";
            
            await _db.ExecuteNonQueryAsync(query, new Dictionary<string, object> { 
                { "@key", key }, 
                { "@value", value } 
            });
        }

        public async Task<decimal> GetTaxRateAsync()
        {
            var val = await GetSettingAsync("tax_rate", "0.0");
            return decimal.TryParse(val, out var res) ? res : 0m;
        }
    }
}
