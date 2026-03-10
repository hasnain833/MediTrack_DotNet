using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using DChemist.Services;
using DChemist.Repositories;
using DChemist.Database;
using DChemist.ViewModels;
using DChemist.Utils;
using Microsoft.Extensions.Configuration;

namespace DChemist
{
    public partial class App : Application
    {
        public IServiceProvider Services { get; }
        public static new App Current => (App)Application.Current;

        public static FrameworkElement? MainRoot { get; private set; }
        public Window? MainWindow => _window;
        private Window? _window;

        public App()
        {
            // Set up early exception handling
            this.UnhandledException += async (s, e) =>
            {
                e.Handled = true;
                AppLogger.LogError($"Unhandled exception: {e.Message}", e.Exception);

                // Show a safe dialog so the user knows something went wrong
                try
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Unexpected Error",
                        Content = "An unexpected error occurred. The application will continue.\n\nDetails have been saved to the log file.",
                        CloseButtonText = "OK"
                    };
                    if (MainRoot?.XamlRoot != null)
                    {
                        dialog.XamlRoot = MainRoot.XamlRoot;
                        await dialog.ShowAsync();
                    }
                }
                catch { /* Dialog itself failed — already logged above */ }
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            
            // Core Config
            services.AddSingleton<IConfiguration>(configuration);
            services.AddSingleton<DatabaseService>(); 

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
            services.AddSingleton<IFiscalService, FiscalService>();
            services.AddSingleton<IPrintService, ThermalPrintService>();
            services.AddSingleton<IReportingService, ReportingService>();
            services.AddSingleton<UpdateService>();
            services.AddSingleton<InventoryEventBus>();

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
            navService.Navigate("DChemist.Views.LoginPage");
            
            _window.Activate();
        }
    }
}
