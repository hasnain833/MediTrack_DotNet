using System;

namespace DChemist.Utils
{
    public static class BillingTotals
    {
        public static (decimal Discount, decimal Tax, decimal Extra, decimal Total) Calculate(
            decimal subtotal, decimal taxRate, decimal flatDiscount, decimal percentage,
            bool usePercentage, decimal extra)
        {
            decimal discount = usePercentage
                ? subtotal * Math.Clamp(percentage, 0, 100) / 100
                : Math.Clamp(flatDiscount, 0, subtotal);
            decimal tax = subtotal * taxRate;
            extra = Math.Max(0, extra);
            return (discount, tax, extra, subtotal + tax - discount + extra);
        }
    }
}
