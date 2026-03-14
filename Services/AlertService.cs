using System;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Utils;
using Npgsql;

namespace DChemist.Services
{
    public class AlertService
    {
        private readonly DatabaseService _db;
        private readonly IDialogService _dialogService;
        private readonly AuthorizationService _auth;
        private bool _hasShownAlertsThisSession = false;

        public AlertService(DatabaseService db, IDialogService dialogService, AuthorizationService auth)
        {
            _db = db;
            _dialogService = dialogService;
            _auth = auth;
        }

        public async Task CheckAndShowAlertsAsync()
        {
            if (_hasShownAlertsThisSession) return;
            if (!_auth.IsAdmin) return; // Only show structural alerts to admins.

            try
            {
                using var conn = _db.GetConnection();
                await conn.OpenAsync();

                // Check low stock
                using var lowStockCmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM (
                        SELECT medicine_id FROM inventory_batches
                        GROUP BY medicine_id
                        HAVING COALESCE(SUM(remaining_units), 0) < 10
                    ) AS low_stock", conn);
                
                var lowStockCount = Convert.ToInt64(await lowStockCmd.ExecuteScalarAsync() ?? 0);

                // Check expiring
                using var expiringCmd = new NpgsqlCommand(@"
                    SELECT COUNT(*) FROM inventory_batches 
                    WHERE expiry_date <= CURRENT_DATE + INTERVAL '90 days' 
                    AND remaining_units > 0", conn);
                var expiringCount = Convert.ToInt64(await expiringCmd.ExecuteScalarAsync() ?? 0);

                if (lowStockCount > 0 || expiringCount > 0)
                {
                    var msg = "You have items needing attention:\n\n";
                    if (lowStockCount > 0) msg += $"• {lowStockCount} medicines have low stock (less than 10 units).\n";
                    if (expiringCount > 0) msg += $"• {expiringCount} batches will expire within 90 days.";

                    await _dialogService.ShowMessageAsync("Inventory Alerts", msg);
                }

                _hasShownAlertsThisSession = true; // Prevent nagging on every page reload
            }
            catch (Exception ex)
            {
                AppLogger.LogError("AlertService failed to check alerts.", ex);
            }
        }

        public void ResetSession()
        {
            _hasShownAlertsThisSession = false;
        }
    }
}
