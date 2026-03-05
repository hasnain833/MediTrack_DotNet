using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MediTrack.Models;
using MediTrack.Repositories;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class UserManagementViewModel : ViewModelBase
    {
        private readonly UserRepository _userRepository;
        private bool _isBusy;

        public UserManagementViewModel(UserRepository userRepository)
        {
            _userRepository = userRepository;
            Users = new ObservableCollection<User>();
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            
            _ = RefreshAsync();
        }

        public ObservableCollection<User> Users { get; }
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
        public ICommand RefreshCommand { get; }

        private async Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                var list = await _userRepository.GetAllUsersAsync();
                Users.Clear();
                foreach (var item in list) Users.Add(item);
            }
            finally { IsBusy = false; }
        }
    }
}
