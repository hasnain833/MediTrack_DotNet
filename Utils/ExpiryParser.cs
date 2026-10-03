using System;
using System.Linq;

namespace DChemist.Utils
{
    /// <summary>
    /// Pharmacy expiry input: "MMYY" / "MM/YY", "MMYYYY" / "MM/YYYY" (= last day of that month),
    /// or "DDMMYYYY" / "DD/MM/YYYY". Separators are ignored.
    /// </summary>
    public static class ExpiryParser
    {
        public static DateTime? Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string d = new string(text.Where(char.IsDigit).ToArray());
            try
            {
                return d.Length switch
                {
                    4 => EndOfMonth(2000 + int.Parse(d[2..4]), int.Parse(d[0..2])),
                    6 => EndOfMonth(int.Parse(d[2..6]), int.Parse(d[0..2])),
                    8 => new DateTime(int.Parse(d[4..8]), int.Parse(d[2..4]), int.Parse(d[0..2])),
                    _ => null
                };
            }
            catch (ArgumentOutOfRangeException) { return null; } // e.g. month 13
        }

        /// <summary>Display form matching what was typed: dd/MM/yyyy for full dates, else MM/yyyy.</summary>
        public static string Format(DateTime date, bool hasDay) => date.ToString(hasDay ? "dd/MM/yyyy" : "MM/yyyy");

        public static bool HasDay(string text) => text.Count(char.IsDigit) == 8;

        private static DateTime EndOfMonth(int year, int month) => new DateTime(year, month, DateTime.DaysInMonth(year, month));
    }
}
