using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DChemist.Models;
using DChemist.Utils;
using System.Linq;

namespace DChemist.Services
{
    public class BarcodeLookupService
    {
        private readonly HttpClient _httpClient;
        private const string UpcItemDbUrl = "https://api.upcitemdb.com/prod/trial/lookup?upc=";

        public BarcodeLookupService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(10);
            
            // UPCitemdb expects a User-Agent in many cases
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("DChemist/1.0");
        }

        public async Task<Medicine?> FetchMedicineFromExternalApiAsync(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;

            try
            {
                var response = await _httpClient.GetAsync(UpcItemDbUrl + barcode.Trim());
                if (!response.IsSuccessStatusCode)
                {
                    AppLogger.LogWarning($"External API lookup failed for {barcode}. Status: {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync();
                
                // Use JsonDocument for quick dynamic parsing
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;
                
                if (root.TryGetProperty("code", out var codeElement) && codeElement.GetString() == "OK")
                {
                    if (root.TryGetProperty("items", out var itemsElement) && itemsElement.GetArrayLength() > 0)
                    {
                        var firstItem = itemsElement[0];
                        
                        var name = firstItem.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "Unknown Product";
                        var brand = firstItem.TryGetProperty("brand", out var brandProp) ? brandProp.GetString() : "Unknown Brand";
                        var description = firstItem.TryGetProperty("description", out var descProp) ? descProp.GetString() : "";

                        AppLogger.LogInfo($"External API found product: {name} (Brand: {brand}) for {barcode}");

                        return new Medicine
                        {
                            Name = ExtractShortName(name) ?? "Unknown Product",
                            GenericName = description ?? string.Empty,
                            Barcode = barcode,
                            ManufacturerName = brand,
                            CategoryName = "General",
                            Strength = "" // Hard to reliably extract strength from standard UPC databases
                        };
                    }
                }
                
                AppLogger.LogInfo($"External API returned 200 OM but empty items for barcode {barcode}. Content: {content}");
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Error calling external API for barcode {barcode}", ex);
                return null;
            }
        }
        
        /// <summary>
        /// UPCitemdb titles are often very long (e.g. "Advil Ibuprofen 200mg Pain Reliever, 50 Coated Tablets").
        /// We try to truncate it to something reasonable for a Name field if it's over 100 characters.
        /// </summary>
        private string? ExtractShortName(string? fullname)
        {
            if (string.IsNullOrWhiteSpace(fullname)) return fullname;
            if (fullname.Length <= 100) return fullname;
            
            // Truncate to first 100 chars, try to end at a word boundary
            var truncated = fullname.Substring(0, 100);
            var lastSpace = truncated.LastIndexOf(' ');
            if (lastSpace > 50) return truncated.Substring(0, lastSpace) + "...";
            return truncated + "...";
        }
    }
}
