using System;

namespace DChemist.Models
{
    public class AlertBatchItem
    {
        public int MedicineId { get; set; }
        public int? BatchId { get; set; }
        public string MedicineName { get; set; } = string.Empty;
        public int TotalUnits { get; set; }
        public DateTime? ExpiryDate { get; set; }

        // Helper for display
        public string ExpiryDisplay => ExpiryDate.HasValue
            ? ExpiryDate.Value.ToString("MMM d, yyyy")
            : string.Empty;

        public string StockDisplay => $"{TotalUnits} units";

        public int DaysLeft => ExpiryDate.HasValue
            ? (int)(ExpiryDate.Value - DateTime.Today).TotalDays
            : int.MaxValue;

        public string DaysLeftDisplay => DaysLeft <= 0 ? "EXPIRED" : $"{DaysLeft}d left";
    }
}
