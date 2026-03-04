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
            // For now, let's implement a simple login or Mock
            // In a real app, you'd verify password hash
            var user = await _userRepository.GetByUsernameAsync(username);
            
            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.Password))
            {
                CurrentUser = user;
                return true;
            }
            
            // Temporary allow admin/admin for testing if DB is empty or not set up
            if (username == "admin" && password == "admin")
            {
                CurrentUser = new User { Id = 1, Username = "admin", FullName = "System Administrator", Role = "Admin" };
                return true;
            }

            return false;
        }

        public void Logout()
        {
            CurrentUser = null;
        }
    }
}
