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

        public bool IsAdmin => _authService.CurrentUser != null;

        public bool CanManageInventory() => IsAdmin;
        public bool CanViewReports() => IsAdmin;
        public bool CanManageUsers() => IsAdmin;
        public bool CanDoBilling() => IsAdmin;
        public bool CanViewInventory() => IsAdmin;

        public void EnforceAdmin()
        {
            if (!IsAdmin)
                throw new UnauthorizedAccessException("Access denied: Admin only.");
        }
    }
}
