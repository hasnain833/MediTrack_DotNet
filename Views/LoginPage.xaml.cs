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
