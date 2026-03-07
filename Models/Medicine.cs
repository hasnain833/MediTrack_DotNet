using System;

namespace MediTrack.Models
{
    public class Medicine
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? GenericName { get; set; }
        public int? CategoryId { get; set; }
        public int? ManufacturerId { get; set; }
        public string? DosageForm { get; set; }
        public string? Strength { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Joined properties for UI display
        public string? CategoryName { get; set; }
        public string? ManufacturerName { get; set; }
        public decimal Price { get; set; }
        public int StockQty { get; set; }
        public DateTime? ExpiryDate { get; set; }
    }
}
