using System.Threading.Tasks;
using System.Windows.Input;
using MediTrack.Services;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly AuthService _authService;
        private readonly NavigationService _navigationService;
        private string _username = string.Empty;
        private string _password = string.Empty;
        private string? _errorMessage;
        private bool _isBusy;

        public LoginViewModel(AuthService authService, NavigationService navigationService)
        {
            _authService = authService;
            _navigationService = navigationService;
            LoginCommand = new RelayCommand(async _ => await ExecuteLoginAsync(), _ => CanLogin());
        }

        public string Username
        {
            get => _username;
            set { if (SetProperty(ref _username, value)) ((RelayCommand)LoginCommand).RaiseCanExecuteChanged(); }
        }

        public string Password
        {
            get => _password;
            set { if (SetProperty(ref _password, value)) ((RelayCommand)LoginCommand).RaiseCanExecuteChanged(); }
        }

        public string? ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }
        public bool IsBusy { get => _isBusy; set { if (SetProperty(ref _isBusy, value)) ((RelayCommand)LoginCommand).RaiseCanExecuteChanged(); } }

        public ICommand LoginCommand { get; }

        private bool CanLogin() => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password) && !IsBusy;

        private async Task ExecuteLoginAsync()
        {
            ErrorMessage = null;
            IsBusy = true;

            try
            {
                bool success = await _authService.LoginAsync(Username, Password);
                if (success)
                {
                    _navigationService.Navigate("MediTrack.Views.MainPage");
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex)
            {
                ErrorMessage = $"Database Error: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                ErrorMessage = $"Critical Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
