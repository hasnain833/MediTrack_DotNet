using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Utils;
using Npgsql;

namespace DChemist.Services
{
    public class BackupService
    {
        private readonly IConfiguration _config;
        private readonly IDialogService _dialogService;
        private readonly AuthorizationService _auth;

        public BackupService(IConfiguration config, IDialogService dialogService, AuthorizationService auth)
        {
            _config = config;
            _dialogService = dialogService;
            _auth = auth;
        }

        public async Task RunBackupAsync()
        {
            if (!_auth.IsAdmin)
            {
                await _dialogService.ShowMessageAsync("Access Denied", "Only administrators can perform database backups.");
                return;
            }

            try
            {
                string connectionString = _config.GetConnectionString("DefaultConnection") ?? "";
                if (string.IsNullOrEmpty(connectionString)) throw new Exception("Database connection string not found.");

                var builder = new NpgsqlConnectionStringBuilder(connectionString);
                
                // Allow user to pick where to save the backup
                var picker = new Windows.Storage.Pickers.FileSavePicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = $"DChemist_Backup_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
                picker.FileTypeChoices.Add("SQL File", new System.Collections.Generic.List<string>() { ".sql" });

                var file = await picker.PickSaveFileAsync();
                if (file == null) return; // User cancelled

                var backupFilePath = file.Path;

                // Try to find pg_dump.exe (common default paths)
                string pgDumpPath = _config["Database:PgDumpPath"] ?? @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe";
                if (!File.Exists(pgDumpPath))
                {
                    pgDumpPath = @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"; // Fallback to v15
                }
                if (!File.Exists(pgDumpPath))
                {
                    await _dialogService.ShowMessageAsync("Configuration Error", "pg_dump.exe not found. Please set Database:PgDumpPath in appsettings.json.");
                    return;
                }

                // Prepare process info
                var psi = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-h {builder.Host} -p {builder.Port} -U {builder.Username} -d {builder.Database} -f \"{backupFilePath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Provide password via environment variable (standard approach for pg_dump)
                psi.EnvironmentVariables["PGPASSWORD"] = builder.Password;

                var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();

                    if (process.ExitCode == 0)
                    {
                        await _dialogService.ShowMessageAsync("Success", $"Database backup completed successfully.\nSaved to: {backupFilePath}");
                    }
                    else
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        throw new Exception($"Backup failed with exit code {process.ExitCode}: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.RunBackupAsync failed", ex);
                await _dialogService.ShowMessageAsync("Backup Error", ex.Message);
            }
        }

        public async Task RestoreDatabaseAsync()
        {
            if (!_auth.IsAdmin)
            {
                await _dialogService.ShowMessageAsync("Access Denied", "Only administrators can restore database backups.");
                return;
            }

            bool confirm = await _dialogService.ShowConfirmationAsync(
                "Warning: Database Restore",
                "Restoring a backup will OVERWRITE all current data with the backup contents. This cannot be undone. Are you sure you want to proceed?",
                "Restore", "Cancel");

            if (!confirm) return;

            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.MainWindow);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add(".sql");

                var file = await picker.PickSingleFileAsync();
                if (file == null) return;

                string psqlPath = _config["Database:PsqlPath"] ?? "psql.exe";
                if (!File.Exists(psqlPath))
                {
                    await _dialogService.ShowMessageAsync("Configuration Error", "psql.exe not found. Please set Database:PsqlPath in appsettings.json.");
                    return;
                }

                var dbConfig = _config.GetSection("Database");
                // Using PGPASSWORD environment variable to avoid interactive prompt
                var psi = new ProcessStartInfo
                {
                    FileName = psqlPath,
                    Arguments = $"-h {dbConfig["Host"]} -p {dbConfig["Port"]} -U {dbConfig["User"]} -d {dbConfig["Database"]} -f \"{file.Path}\"",
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables["PGPASSWORD"] = dbConfig["Password"];

                using var process = Process.Start(psi);
                if (process == null) throw new Exception("Failed to start psql process.");

                string error = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    await _dialogService.ShowMessageAsync("Restore Successful", "The database has been restored from the backup. The application will now restart to ensure consistency.");
                    Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                }
                else
                {
                    throw new Exception($"psql failed with exit code {process.ExitCode}: {error}");
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("BackupService.RestoreDatabaseAsync failed", ex);
                await _dialogService.ShowMessageAsync("Restore Error", ex.Message);
            }
        }
    }
}
