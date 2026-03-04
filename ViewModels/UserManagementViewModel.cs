using System.Collections.ObjectModel;
using System.Threading.Tasks;
using MediTrack.Models;
using MediTrack.Repositories;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly UserRepository _userRepository;

        public UserManagementViewModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
            Users = new ObservableCollection<User>
            {
                new User { Username = "admin", FullName = "System Admin", Role = "Admin", Status = "Active" },
                new User { Username = "cashier1", FullName = "John Doe", Role = "Cashier", Status = "Active" }
            };
        }

        public ObservableCollection<User> Users { get; }
    }
}
