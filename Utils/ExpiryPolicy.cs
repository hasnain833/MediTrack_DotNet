using System;

namespace DChemist.Utils
{
    public static class ExpiryPolicy
    {
        public const int WarningMonths = 6;
        public const string ScopeText = "Expired or expiring within 6 months";
        public static DateTime Cutoff => DateTime.Today.AddMonths(WarningMonths);
        public static bool NeedsAttention(DateTime expiry, int remainingUnits) =>
            remainingUnits > 0 && expiry.Date <= Cutoff;
    }
}
