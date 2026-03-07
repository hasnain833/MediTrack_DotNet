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

        public static FrameworkElement? MainRoot { get; private set; }
        private Window? _window;

        public App()
        {
            // Set up early exception handling
            this.UnhandledException += (s, e) => 
            {
                System.Diagnostics.Debug.WriteLine($"[App] !!! CRITICAL UNHANDLED EXCEPTION: {e.Message}");
                System.Diagnostics.Debug.WriteLine($"[App] Exception Trace: {e.Exception}");
                e.Handled = true; 
            };

            System.Diagnostics.Debug.WriteLine("[App] Constructor: Initializing XAML Components...");
            try 
            {
                this.InitializeComponent();
                System.Diagnostics.Debug.WriteLine("[App] Constructor: XAML Initialized.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App] Constructor: FATAL XAML ERROR: {ex}");
                throw;
            }

            System.Diagnostics.Debug.WriteLine("[App] Constructor: Configuring Dependency Injection...");
            Services = ConfigureServices();
            System.Diagnostics.Debug.WriteLine("[App] Constructor: Services Ready.");
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();
            services.AddSingleton(new DatabaseService()); 

            // Repositories
            services.AddSingleton<UserRepository>();
            services.AddSingleton<MedicineRepository>();
            services.AddSingleton<CustomerRepository>();
            services.AddSingleton<SaleRepository>();
            services.AddSingleton<CategoryRepository>();
            services.AddSingleton<ManufacturerRepository>();
            services.AddSingleton<SupplierRepository>();
            services.AddSingleton<BatchRepository>();

            // Services
            services.AddSingleton<AuthService>();
            services.AddSingleton<AuthorizationService>();
            services.AddSingleton<NavigationService>();

            // ViewModels
            services.AddTransient<LoginViewModel>();
            services.AddTransient<MainViewModel>();
            services.AddTransient<DashboardViewModel>();
            services.AddTransient<InventoryViewModel>();
            services.AddTransient<BillingViewModel>();
            services.AddTransient<FinancialViewModel>();

            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            _window = new MainWindow();
            
            var rootFrame = new Frame();
            MainRoot = rootFrame;
            _window.Content = rootFrame;
            
            var navService = Services.GetRequiredService<NavigationService>();
            navService.InitializeRoot(rootFrame);
            navService.Initialize(rootFrame);
            
            // Navigate to login page initially
            navService.Navigate("MediTrack.Views.LoginPage");
            
            _window.Activate();
        }
    }
}
