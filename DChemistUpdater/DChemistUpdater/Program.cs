using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DChemistUpdater
{
    class Program
    {
        // ── Files that must never be overwritten by the update ────────────────
        private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "appsettings.json",
            "updater.exe",
            "updater.pdb"
        };

        // ── Folders that must never be touched by update or rollback ──────────
        private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "logs",
            "backups",
            "temp",
            "updates"
        };

        // ── Retry settings for locked-file handling ───────────────────────────
        private const int FileRetryCount    = 5;
        private const int FileRetryDelayMs  = 800;

        // ── Global log writer (also writes to console) ────────────────────────
        private static StreamWriter? _logWriter;

        // ── Absolute path of THIS running executable ──────────────────────────
        private static readonly string UpdaterExePath =
            Path.GetFullPath(Process.GetCurrentProcess().MainModule?.FileName ?? "updater.exe");

        static void Main(string[] args)
        {
            // ── Single-instance guard ─────────────────────────────────────────
            using var mutex = new Mutex(true, "DChemistUpdater_Global_Mutex", out bool isNew);
            if (!isNew)
            {
                Console.WriteLine("[ERROR] Another update process is already running. Please wait for it to finish.");
                Thread.Sleep(3000);
                return;
            }

            // ── Argument validation ───────────────────────────────────────────
            if (args.Length < 3)
            {
                Console.WriteLine("[ERROR] Missing arguments.");
                Console.WriteLine("Usage: updater.exe <appPath> <zipPath> <processId>");
                Thread.Sleep(4000);
                return;
            }

            string appPath    = args[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;
            string zipPath    = args[1];
            string backupPath = appPath.TrimEnd(Path.DirectorySeparatorChar) + "_backup"
                                + Path.DirectorySeparatorChar;

            if (!int.TryParse(args[2], out int processId))
            {
                Console.WriteLine("[ERROR] Invalid process ID.");
                Thread.Sleep(3000);
                return;
            }

            // ── Set up log file in %LocalAppData%\D. Chemist\Logs\ ────────────
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "D. Chemist", "Logs");
                Directory.CreateDirectory(logDir);
                string logFile = Path.Combine(logDir,
                    $"updater_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                _logWriter = new StreamWriter(logFile, append: false) { AutoFlush = true };
            }
            catch
            {
                // If log setup fails, proceed without file logging
            }

            Log("================================================");
            Log("  D. Chemist — Automatic Update System v3       ");
            Log("================================================");
            Log($"  Updater   : {UpdaterExePath}");
            Log($"  AppPath   : {appPath}");
            Log($"  ZipPath   : {zipPath}");
            Log($"  TargetPID : {processId}");
            Log("================================================");

            try
            {
                RunUpdate(appPath, zipPath, backupPath, processId);
            }
            catch (Exception ex)
            {
                Log("\n[FATAL] Unexpected error during update:");
                Log(ex.ToString());
                Log("\nPlease contact support if the application fails to start.");
                Thread.Sleep(8000);
            }
            finally
            {
                _logWriter?.Dispose();
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        static void RunUpdate(string appPath, string zipPath, string backupPath, int processId)
        {
            // ── Step 1: Wait for main app to exit ─────────────────────────────
            Log($"\n[1/5] Waiting for D. Chemist (PID {processId}) to exit...");
            WaitForMainAppToExit(processId);

            // Extra delay so the OS / AV / OneDrive releases all file handles.
            // 3 seconds is much safer than 1.5 s, especially on OneDrive paths.
            Log("[INFO] Waiting for OS to release file locks (3 s)...");
            Thread.Sleep(3000);

            // ── Step 2: Validate zip ──────────────────────────────────────────
            Log($"\n[2/5] Validating update package: {zipPath}");
            if (!File.Exists(zipPath))
            {
                Log("[ERROR] Update zip not found. Aborting.");
                Thread.Sleep(4000);
                return;
            }

            if (!IsUpdatePackageValid(zipPath))
            {
                Log("[SECURITY] Invalid update package (DChemist.dll/exe not found). Aborting.");
                Thread.Sleep(4000);
                return;
            }
            Log("[OK] Package is valid.");

            // ── Step 3: Backup current app ────────────────────────────────────
            Log($"\n[3/5] Creating backup at: {backupPath}");
            try
            {
                if (Directory.Exists(backupPath))
                    SafeDeleteDirectory(backupPath);

                CopyDirectory(appPath, backupPath, skipExcluded: true);
                Log("[OK] Backup created.");
            }
            catch (Exception ex)
            {
                Log($"[WARN] Backup partially failed: {ex.Message}");
                Log("[WARN] Proceeding without full backup — rollback may be incomplete.");
            }

            // ── Step 4: Apply update ──────────────────────────────────────────
            Log("\n[4/5] Applying update files...");
            bool updateSucceeded;
            try
            {
                ExtractWithExclusions(zipPath, appPath);
                Log("[OK] Update applied.");
                updateSucceeded = true;
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Update failed: {ex.Message}");
                updateSucceeded = false;
            }

            if (!updateSucceeded)
            {
                Log("[ROLLBACK] Restoring previous version...");
                try
                {
                    CopyDirectory(backupPath, appPath, skipExcluded: true);
                    Log("[OK] Rollback complete.");
                }
                catch (Exception rbEx)
                {
                    Log($"[FATAL] Rollback also failed: {rbEx.Message}");
                    Log("Please reinstall D. Chemist manually.");
                }
                Thread.Sleep(6000);
                return;
            }

            // ── Step 5: Cleanup & Restart ─────────────────────────────────────
            Log("\n[5/5] Cleaning up and restarting...");

            SafeDeleteDirectory(backupPath);

            try { if (File.Exists(zipPath)) File.Delete(zipPath); }
            catch (Exception ex) { Log($"[WARN] Could not delete zip: {ex.Message}"); }

            string exePath = Path.Combine(appPath, "DChemist.exe");
            if (File.Exists(exePath))
            {
                Log($"[OK] Restarting: {exePath}");
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName        = exePath,
                        UseShellExecute = true,
                        WorkingDirectory = appPath
                    };
                    var started = Process.Start(psi);
                    if (started == null || started.HasExited)
                        Log("[WARN] App process may not have started correctly.");
                    else
                        Log($"[OK] App restarted (PID {started.Id}).");
                }
                catch (Exception ex)
                {
                    Log($"[ERROR] Could not restart app: {ex.Message}");
                    Log("Update was applied — please start D. Chemist manually.");
                    Thread.Sleep(5000);
                }
            }
            else
            {
                Log($"[ERROR] DChemist.exe not found at: {exePath}");
                Log("Update was applied but the app could not be restarted. Please start it manually.");
                Thread.Sleep(5000);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Waits up to 12 s for the main app, then kills it if still alive.
        // ─────────────────────────────────────────────────────────────────────
        static void WaitForMainAppToExit(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);

                // Security: only kill recognised D.Chemist processes
                string procName = process.ProcessName.ToLowerInvariant();
                if (!procName.Contains("dchemist") && !procName.Contains("meditrack"))
                {
                    Log($"[SECURITY] PID {processId} ({procName}) is not a recognised D.Chemist process. Aborting.");
                    Thread.Sleep(4000);
                    Environment.Exit(1);
                }

                if (!process.WaitForExit(12000))
                {
                    Log("[WARN] App taking too long to close — terminating...");
                    try { process.Kill(); }
                    catch (Exception ex) { Log($"[WARN] Could not kill process: {ex.Message}"); }

                    // Wait a bit more after kill
                    Thread.Sleep(2000);
                }
            }
            catch (ArgumentException)
            {
                Log("[INFO] Process already exited.");
            }
            catch (Exception ex)
            {
                Log($"[WARN] Error waiting for process: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Validates that the zip contains a known D.Chemist binary.
        // ─────────────────────────────────────────────────────────────────────
        static bool IsUpdatePackageValid(string zipPath)
        {
            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("DChemist.dll", StringComparison.OrdinalIgnoreCase) ||
                        entry.FullName.EndsWith("DChemist.exe", StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception ex)
            {
                Log($"[ERROR] Could not open zip for validation: {ex.Message}");
            }
            return false;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Extracts zip to destinationDir, skipping protected files/folders.
        // Retries locked files up to FileRetryCount times before warning.
        // ─────────────────────────────────────────────────────────────────────
        static void ExtractWithExclusions(string zipPath, string destinationDir)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                // Skip directory-only entries
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // ── Check if the entry lives inside an excluded folder ─────────
                var parts = entry.FullName.Split('/', '\\');
                bool inExcludedFolder = false;
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    if (ExcludedFolders.Contains(parts[i]))
                    {
                        inExcludedFolder = true;
                        break;
                    }
                }
                if (inExcludedFolder)
                {
                    Log($"[SKIP] Excluded folder: {entry.FullName}");
                    continue;
                }

                // ── Skip files on the hard-exclusion list ─────────────────────
                if (ExcludedFiles.Contains(entry.Name))
                {
                    Log($"[SKIP] Excluded file: {entry.Name}");
                    continue;
                }

                // ── Build absolute destination path ───────────────────────────
                string destPath = Path.GetFullPath(
                    Path.Combine(destinationDir,
                        entry.FullName.Replace('/', Path.DirectorySeparatorChar)));

                // ── CRITICAL: Never overwrite the currently running updater ───
                if (IsSameFile(destPath, UpdaterExePath))
                {
                    Log($"[SKIP] Self-overwrite prevented: {entry.Name}");
                    continue;
                }

                // ── Also skip any .pdb companion of the updater ───────────────
                string updaterPdb = Path.ChangeExtension(UpdaterExePath, ".pdb");
                if (IsSameFile(destPath, updaterPdb))
                {
                    Log($"[SKIP] Updater PDB skipped: {entry.Name}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                // ── Extract with retry for locked files ───────────────────────
                bool extracted = false;
                for (int attempt = 1; attempt <= FileRetryCount; attempt++)
                {
                    try
                    {
                        // Use a temp file + atomic replace to avoid partial writes
                        string tempDest = destPath + ".upd_tmp";
                        entry.ExtractToFile(tempDest, overwrite: true);

                        // Delete the target (if it exists) and rename temp → target
                        if (File.Exists(destPath))
                            File.Delete(destPath);

                        File.Move(tempDest, destPath);
                        extracted = true;
                        break;
                    }
                    catch (IOException ex) when (attempt < FileRetryCount)
                    {
                        Log($"[RETRY {attempt}/{FileRetryCount}] {entry.Name} locked — waiting {FileRetryDelayMs} ms... ({ex.Message})");
                        Thread.Sleep(FileRetryDelayMs);
                    }
                    catch (UnauthorizedAccessException ex) when (attempt < FileRetryCount)
                    {
                        Log($"[RETRY {attempt}/{FileRetryCount}] {entry.Name} access denied — waiting {FileRetryDelayMs} ms... ({ex.Message})");
                        Thread.Sleep(FileRetryDelayMs);
                    }
                }

                if (!extracted)
                {
                    Log($"[WARN] Could not extract {entry.Name} after {FileRetryCount} attempts — skipping.");
                }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Recursively copies sourceDir → destinationDir.
        // Skips excluded files/folders and the running updater executable.
        // Retries locked files so rollback is as complete as possible.
        // ─────────────────────────────────────────────────────────────────────
        static void CopyDirectory(string sourceDir, string destinationDir, bool skipExcluded)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source not found: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                if (skipExcluded && ExcludedFiles.Contains(file.Name)) continue;

                string destFile = Path.Combine(destinationDir, file.Name);

                // Never copy the running updater over itself
                if (IsSameFile(file.FullName, UpdaterExePath)) continue;

                bool copied = false;
                for (int attempt = 1; attempt <= FileRetryCount; attempt++)
                {
                    try
                    {
                        file.CopyTo(destFile, overwrite: true);
                        copied = true;
                        break;
                    }
                    catch (IOException) when (attempt < FileRetryCount)
                    {
                        Thread.Sleep(FileRetryDelayMs);
                    }
                    catch (UnauthorizedAccessException) when (attempt < FileRetryCount)
                    {
                        Thread.Sleep(FileRetryDelayMs);
                    }
                }

                if (!copied)
                    Log($"[WARN] Skipped locked file during copy: {file.Name}");
            }

            foreach (var subDir in dir.GetDirectories())
            {
                if (skipExcluded && ExcludedFolders.Contains(subDir.Name)) continue;
                CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name), skipExcluded);
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Deletes a directory tree, ignoring individual locked files.
        // ─────────────────────────────────────────────────────────────────────
        static void SafeDeleteDirectory(string path)
        {
            if (!Directory.Exists(path)) return;
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Partial delete — try file-by-file
                try
                {
                    foreach (var f in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
                    {
                        try { File.Delete(f); } catch { /* ignore locked */ }
                    }
                    try { Directory.Delete(path, recursive: true); } catch { /* ignore */ }
                }
                catch { /* ignore */ }
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // True when two paths resolve to the same file (case-insensitive).
        // ─────────────────────────────────────────────────────────────────────
        static bool IsSameFile(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try
            {
                return string.Equals(
                    Path.GetFullPath(a),
                    Path.GetFullPath(b),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Writes a message to both Console and the log file.
        // ─────────────────────────────────────────────────────────────────────
        static void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(line);
            try { _logWriter?.WriteLine(line); } catch { }
        }
    }
}
