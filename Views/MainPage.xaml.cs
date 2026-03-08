using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;

namespace DChemist.Views
{
    public sealed partial class MainPage : Page
    {
        public MainViewModel ViewModel { get; }

        public MainPage()
        {
            Debug.WriteLine("[MainPage] Constructor started."); // Added log
            ViewModel = App.Current.Services.GetRequiredService<MainViewModel>();
            this.InitializeComponent();
            var navService = App.Current.Services.GetRequiredService<DChemist.Services.NavigationService>();
            navService.Initialize(ContentFrame);
            if (ViewModel.NavigationItems.Count > 0)
            {
                ViewModel.SelectedItem = ViewModel.NavigationItems[0];
            }
            Debug.WriteLine("[MainPage] Constructor finished."); // Added log
        }
    }
}
