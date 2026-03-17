using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using DChemist.Database;
using Npgsql;

namespace DChemist.Services
{
    public class SettingsService
    {
        private readonly DatabaseService _db;
        private readonly ConcurrentDictionary<string, string> _cache = new ConcurrentDictionary<string, string>();

        public SettingsService(DatabaseService db)
        {
            _db = db;
        }

        public async Task<string> GetSettingAsync(string key, string defaultValue = "")
        {
            if (_cache.TryGetValue(key, out var cachedValue))
            {
                return cachedValue;
            }

            const string query = "SELECT value FROM settings WHERE key = @key";
            var result = await _db.FetchOneAsync(query, reader => reader.GetString(0), new Dictionary<string, object> { { "@key", key } });
            
            var finalValue = result ?? defaultValue;
            _cache.TryAdd(key, finalValue);
            return finalValue;
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

            // Update or add to cache
            _cache.AddOrUpdate(key, value, (k, old) => value);
        }

        public async Task<decimal> GetTaxRateAsync()
        {
            var val = await GetSettingAsync("tax_rate", "0.0");
            return decimal.TryParse(val, out var res) ? res : 0m;
        }

        public async Task<string> GetPharmacyNameAsync() => await GetSettingAsync("pharmacy_name", "D. Chemist");
        public async Task<string> GetPharmacyAddressAsync() => await GetSettingAsync("pharmacy_address", "Khewra Road, Choa Saidan Shah, District Chakwal");
        public async Task<string> GetPharmacyPhoneAsync() => await GetSettingAsync("pharmacy_phone", "+92-332-8787833");
        public async Task<string> GetPharmacyLicenseAsync() => await GetSettingAsync("pharmacy_license", "01-372-0011-134212M");
        public async Task<string> GetPharmacyNtnAsync() => await GetSettingAsync("pharmacy_ntn", "I736466-5");
        
        public async Task<bool> IsAutoBackupEnabledAsync()
        {
            var val = await GetSettingAsync("auto_backup_enabled", "true");
            return val.ToLower() == "true";
        }

        public async Task<string> GetPrinterNameAsync() => await GetSettingAsync("printer_name", "");
        
        public async Task<bool> IsSilentPrintEnabledAsync()
        {
            var val = await GetSettingAsync("silent_print_enabled", "false");
            return val.ToLower() == "true";
        }
    }
}
