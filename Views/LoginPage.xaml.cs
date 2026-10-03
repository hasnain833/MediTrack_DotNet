using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace DChemist.Views
{
    public sealed partial class LoginPage : Page
    {
        public LoginViewModel ViewModel { get; }

        public LoginPage()
        {
            System.Diagnostics.Debug.WriteLine("[LoginPage] Constructor: Resolving ViewModel...");
            ViewModel = App.Current.Services.GetRequiredService<LoginViewModel>();

            System.Diagnostics.Debug.WriteLine("[LoginPage] Constructor: Initializing XAML Components...");
            try 
            {
                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[LoginPage] Constructor: XAML Initialized.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LoginPage] Constructor: FATAL XAML ERROR: {ex}");
                throw;
            }
        }

        public string AppVersion => typeof(App).Assembly.GetName().Version?.ToString() ?? string.Empty;

        protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            DispatcherQueue.TryEnqueue(() => UsernameBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic));
        }

        // Enter in Username moves to Password (it used to try to sign in with an empty password).
        private void OnUsernameKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter) return;
            PasswordBox.Focus(Microsoft.UI.Xaml.FocusState.Programmatic);
            e.Handled = true;
        }

        private void OnKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                if (ViewModel.LoginCommand.CanExecute(null))
                {
                    ViewModel.LoginCommand.Execute(null);
                }
            }
        }
    }
}
