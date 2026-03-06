using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace MediTrack.Views
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: Resolving ViewModel...");
            ViewModel = App.Current.Services.GetRequiredService<DashboardViewModel>();

            System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: Initializing XAML Components...");
            try 
            {
                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[DashboardPage] Constructor: XAML Initialized.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardPage] Constructor: FATAL XAML ERROR: {ex}");
                throw;
            }
        }
    }
}
