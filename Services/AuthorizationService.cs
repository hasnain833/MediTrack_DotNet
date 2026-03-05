using System;
using MediTrack.Models;

namespace MediTrack.Services
{
    public class AuthorizationService
    {
        private readonly AuthService _authService;

        public AuthorizationService(AuthService authService)
        {
            _authService = authService;
        }

        public bool IsAdmin => _authService.CurrentUser?.Role == "Admin";
        public bool IsCashier => _authService.CurrentUser?.Role == "Cashier";

        public bool CanManageInventory() => IsAdmin;
        public bool CanViewReports() => IsAdmin;
        public bool CanManageUsers() => IsAdmin;
        public bool CanDoBilling() => IsAdmin || IsCashier;
        public bool CanViewInventory() => IsAdmin || IsCashier;

        public void EnforceAdmin()
        {
            if (!IsAdmin)
                throw new UnauthorizedAccessException("Access denied: Admin only.");
        }

        public void EnforceCashierOrAdmin()
        {
            if (!IsAdmin && !IsCashier)
                throw new UnauthorizedAccessException("Access denied: Unauthorized.");
        }
    }
}
