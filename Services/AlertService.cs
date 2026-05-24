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
                // Logic removed as per user request (no startup alerts)
                _hasShownAlertsThisSession = true;
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
