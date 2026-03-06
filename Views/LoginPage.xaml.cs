using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MediTrack.Views
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
    }
}
