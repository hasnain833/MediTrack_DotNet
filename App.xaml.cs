using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using MediTrack.Services;
using MediTrack.Repositories;
using MediTrack.Database;
using MediTrack.ViewModels;

namespace MediTrack
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public static new App Current => (App)Application.Current;

        private Window? _window;

        public App()
        {
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // Core - UPDATE DATABASE CREDENTIALS HERE
            // If you have a MySQL root password, enter it in the 4th parameter below
            services.AddSingleton(new DatabaseService("localhost", "medical_store", "root", "H")); 

            // Repositories
            services.AddSingleton<UserRepository>();
            services.AddSingleton<MedicineRepository>();
            services.AddSingleton<CustomerRepository>();
            services.AddSingleton<SaleRepository>();

            // Services
            services.AddSingleton<AuthService>();
            services.AddSingleton<NavigationService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<BillingViewModel>();
            services.AddTransient<FinancialViewModel>();
            services.AddTransient<UserManagementViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            
            var rootFrame = new Frame();
            _window.Content = rootFrame;
            
            var navService = Services.GetRequiredService<NavigationService>();
            navService.Initialize(rootFrame);
            
            // Navigate to login page initially
            navService.Navigate("MediTrack.Views.LoginPage");
            
            _window.Activate();
        }
    }
}
