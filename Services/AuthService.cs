using System;
using System.Threading.Tasks;
using MediTrack.Models;
using MediTrack.Repositories;

namespace MediTrack.Services
{
    public class AuthService
    {
        private readonly UserRepository _userRepository;

        public User? CurrentUser { get; private set; }

        public AuthService(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            // 1. Try DB login
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user != null)
            {
                try 
                {
                    if (BCrypt.Net.BCrypt.Verify(password, user.Password))
                    {
                        CurrentUser = user;
                        return true;
                    }
                }
                catch { /* Hash might be invalid format, fallback to manual checks below */ }
            }
            
            // 2. Fallback for the default admin during setup/debug
            if (username.ToLower() == "admin")
            {
                if (password == "admin123" || password == "admin")
                {
                    CurrentUser = new User 
                    { 
                        Id = 1, 
                        Username = "admin", 
                        FullName = "System Administrator", 
                        Role = "Admin",
                        Status = "Active"
                    };
                    return true;
                }
            }
            
            return false;
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}
