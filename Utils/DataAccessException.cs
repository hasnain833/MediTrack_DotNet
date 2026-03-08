using System;

namespace DChemist.Utils
{
    /// <summary>
    /// Thrown by repositories when a database operation fails.
    /// Carries a clean user-facing message separately from the technical detail.
    /// ViewModels catch this and show Message to the user without exposing stack traces.
    /// </summary>
    public class DataAccessException : Exception
    {
        public DataAccessException(string userMessage, Exception? inner = null)
            : base(userMessage, inner)
        {
        }
    }
}
