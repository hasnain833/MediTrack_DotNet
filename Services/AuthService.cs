using System;
using System.Threading.Tasks;
using DChemist.Models;
using DChemist.Repositories;
using DChemist.Utils;

namespace DChemist.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;
        private readonly AuditRepository _auditRepo;

        public User? CurrentUser { get; private set; }

        public AuthService(UserRepository userRepository, AuditRepository auditRepo)
        {
            _userRepository = userRepository;
            _auditRepo = auditRepo;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user != null)
            {
                try 
                {
                    if (BCrypt.Net.BCrypt.Verify(password, user.Password))
                    {
                        CurrentUser = user;
                        await _auditRepo.InsertLogAsync(user.Id, "Login", $"User {user.Username} logged in successfully.");
                        return true;
                    }
                }
                catch { /* Hash might be invalid format */ }
            }
            
            return false;
        }

        public async Task LogoutAsync()
        {
            if (CurrentUser != null)
            {
                await _auditRepo.InsertLogAsync(CurrentUser.Id, "Logout", "User logged out.");
            }
            CurrentUser = null;
        }

        public async Task<bool> ChangePasswordAsync(string newPassword)
        {
            if (CurrentUser == null) return false;

            var hashed = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _userRepository.UpdatePasswordAsync(CurrentUser.Id, hashed);
            
            CurrentUser.MustChangePassword = false;
            await _auditRepo.InsertLogAsync(CurrentUser.Id, "Security", "Password changed successfully.");
            return true;
        }
    }
}
