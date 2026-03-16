using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Utils;
using DChemist.Repositories;
using Npgsql;

namespace DChemist.Services
{
    public class BackupService
    {
        private readonly IConfiguration _config;
        private readonly IDialogService _dialogService;
        private readonly AuthorizationService _auth;
        private readonly AuditRepository _auditRepo;
        private readonly SettingsService _settings;

        public BackupService(IConfiguration config, IDialogService dialogService, AuthorizationService auth, AuditRepository auditRepo, SettingsService settings)
        {
            _config = config;
            _dialogService = dialogService;
            _auth = auth;
            _auditRepo = auditRepo;
            _settings = settings;
        }

        public async Task CheckAndRunScheduledBackupAsync()
        {
            try
            {
                if (!await _settings.IsAutoBackupEnabledAsync()) return;

                string lastBackup = await _settings.GetSettingAsync("last_backup_date", "");
                if (DateTime.TryParse(lastBackup, out DateTime lastDate))
                {
                    if (lastDate.Date == DateTime.Today) return; // Already backed up today
                }

                // Perform auto backup to dedicated folder
                await RunAutoBackupAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Scheduled backup check failed", ex);
            }
        }

        private async Task RunAutoBackupAsync()
        {
            try
            {
                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);

                string fileName = $"backup_{DateTime.Now:yyyy_MM_dd}.sql";
                string fullPath = Path.Combine(backupDir, fileName);

                var dbConfig = _config.GetSection("Database");
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = dbConfig["Host"],
                    Port = int.Parse(dbConfig["Port"] ?? "5432"),
                    Database = dbConfig["Database"],
                    Username = dbConfig["User"],
                    Password = dbConfig["Password"],
                    Pooling = true
                };
                
                string host = string.IsNullOrEmpty(builder.Host) ? "localhost" : builder.Host;
                int port = builder.Port > 0 ? builder.Port : 5432;

                string pgDumpPath = GetPgDumpPath();
                if (string.IsNullOrEmpty(pgDumpPath)) return;

                var psi = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-h \"{host}\" -p {port} -U \"{builder.Username}\" -d \"{builder.Database}\" -f \"{fullPath}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                psi.EnvironmentVariables["PGPASSWORD"] = builder.Password;

                var process = Process.Start(psi);
                if (process != null)
                {
                    await process.WaitForExitAsync();
                    if (process.ExitCode == 0)
                    {
                        AppLogger.LogInfo($"Auto-backup successful: {fileName}");
                        await _settings.SaveSettingAsync("last_backup_date", DateTime.Today.ToString("yyyy-MM-dd"));
                        await _auditRepo.InsertLogAsync(0, "System", $"Automatic database backup created: {fileName}");
                        
                        // Enforce 7-day retention
                        CleanupOldBackups(backupDir);
                    }
                    else
                    {
                        string error = await process.StandardError.ReadToEndAsync();
                        AppLogger.LogError($"Auto-backup failed: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("RunAutoBackupAsync failed", ex);
            }
        }

        private void CleanupOldBackups(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, "backup_*.sql");
                if (files.Length > 7)
                {
                    var oldFiles = files.Select(f => new FileInfo(f))
                                        .OrderByDescending(f => f.CreationTime)
                                        .Skip(7);
                    
                    foreach (var file in oldFiles)
                    {
                        file.Delete();
                        AppLogger.LogInfo($"Deleted old backup: {file.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogError("CleanupOldBackups failed", ex);
            }
        }

        private string GetPgDumpPath()
        {
            string pgDumpPath = _config["Database:PgDumpPath"] ?? @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe";
            if (File.Exists(pgDumpPath)) return pgDumpPath;
            
            pgDumpPath = @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe";
            if (File.Exists(pgDumpPath)) return pgDumpPath;

            return string.Empty;
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
                var dbConfig = _config.GetSection("Database");
                var builder = new NpgsqlConnectionStringBuilder
                {
                    Host = dbConfig["Host"],
                    Port = int.Parse(dbConfig["Port"] ?? "5432"),
                    Database = dbConfig["Database"],
                    Username = dbConfig["User"],
                    Password = dbConfig["Password"],
                    Pooling = true
                };
                
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
