using System;

namespace DChemist.Models
{
    public class FinancialReport
    {
        public DateTime ReportDate { get; set; }
        public decimal GrossSales { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal TotalReturns { get; set; }
        public decimal NetSales { get; set; }
        
        public int TotalSalesCount { get; set; }
        public int FbrSalesCount { get; set; }
        public int InternalSalesCount { get; set; }
        public int ReturnsCount { get; set; }
    }
}
