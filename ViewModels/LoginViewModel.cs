using System.Threading.Tasks;
using System.Windows.Input;
using DChemist.Services;
using DChemist.Utils;
using Npgsql;
using DChemist.Models;

namespace DChemist.ViewModels
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
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] ExecuteLoginAsync: Attempting login for user '{Username}'");
            ErrorMessage = null;
            IsBusy = true;

            try
            {
                bool success = await _authService.LoginAsync(Username, Password);
                if (success)
                {
                    System.Diagnostics.Debug.WriteLine("[LoginViewModel] ExecuteLoginAsync: Login SUCCESS. Navigating to MainPage...");
                    _navigationService.Navigate("DChemist.Views.MainPage");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[LoginViewModel] ExecuteLoginAsync: Login FAILED (Invalid credentials).");
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (NpgsqlException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginViewModel] ExecuteLoginAsync: DATABASE ERROR: {ex.Message}");
                ErrorMessage = $"Database Error: {ex.Message}";
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginViewModel] ExecuteLoginAsync: CRITICAL ERROR: {ex}");
                ErrorMessage = $"Critical Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
