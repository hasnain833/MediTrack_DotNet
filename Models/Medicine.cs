using System;

namespace MediTrack.Models
{
    public class Medicine
    {
        public int Id { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQty { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Supplier { get; set; } = string.Empty;
    }
}
