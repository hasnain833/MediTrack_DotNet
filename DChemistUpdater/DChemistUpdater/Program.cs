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
        // ── Files/folders that must NEVER be overwritten on update ───────────
        private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
        {
            "appsettings.json",
            "updater.exe",
            "updater.pdb"
        };

        private static readonly HashSet<string> ExcludedFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            "logs",
            "backups",
            "temp",
            "updates"
        };

        static void Main(string[] args)
        {
            Console.WriteLine("================================================");
            Console.WriteLine("  D. Chemist — Automatic Update System v2       ");
            Console.WriteLine("================================================");

            if (args.Length < 3)
            {
                Console.WriteLine("[ERROR] Missing arguments.");
                Console.WriteLine("Usage: updater.exe <appPath> <zipPath> <processId>");
                Thread.Sleep(4000);
                return;
            }

            string appPath  = args[0].TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string zipPath  = args[1];
            string backupPath = appPath.TrimEnd(Path.DirectorySeparatorChar) + "_backup" + Path.DirectorySeparatorChar;

            if (!int.TryParse(args[2], out int processId))
            {
                Console.WriteLine("[ERROR] Invalid process ID.");
                Thread.Sleep(3000);
                return;
            }

            try
            {
                // ── Step 1: Wait for the main app to exit ────────────────────
                Console.WriteLine($"[1/5] Waiting for D. Chemist (PID {processId}) to exit...");
                try
                {
                    var process = Process.GetProcessById(processId);
                    string procName = process.ProcessName.ToLowerInvariant();
                    if (!procName.Contains("dchemist") && !procName.Contains("meditrack"))
                    {
                        Console.WriteLine($"[SECURITY] PID {processId} ({procName}) is not a recognized D.Chemist process. Aborting.");
                        Thread.Sleep(4000);
                        return;
                    }

                    if (!process.WaitForExit(12000))
                    {
                        Console.WriteLine("[WARN] App taking too long to close — terminating...");
                        process.Kill();
                    }
                }
                catch (ArgumentException)
                {
                    Console.WriteLine("[INFO] Process already exited.");
                }

                Thread.Sleep(1500); // Let OS release file locks

                // ── Step 2: Validate zip ─────────────────────────────────────
                Console.WriteLine($"[2/5] Validating update package: {zipPath}");
                if (!File.Exists(zipPath))
                {
                    Console.WriteLine("[ERROR] Update zip not found. Aborting.");
                    Thread.Sleep(4000);
                    return;
                }

                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    bool valid = false;
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.EndsWith("DChemist.dll", StringComparison.OrdinalIgnoreCase) ||
                            entry.FullName.EndsWith("DChemist.exe", StringComparison.OrdinalIgnoreCase))
                        {
                            valid = true;
                            break;
                        }
                    }
                    if (!valid)
                    {
                        Console.WriteLine("[SECURITY] Invalid update package (DChemist.dll/exe not found). Aborting.");
                        Thread.Sleep(4000);
                        return;
                    }
                }

                Console.WriteLine("[OK] Package is valid.");

                // ── Step 3: Backup current app ───────────────────────────────
                Console.WriteLine($"[3/5] Creating backup at: {backupPath}");
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, true);

                CopyDirectory(appPath, backupPath, skipExcluded: false);
                Console.WriteLine("[OK] Backup created.");

                // ── Step 4: Apply update (selective extract) ─────────────────
                Console.WriteLine("[4/5] Applying update files...");
                try
                {
                    ExtractWithExclusions(zipPath, appPath);
                    Console.WriteLine("[OK] Update applied.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Update failed: {ex.Message}");
                    Console.WriteLine("[ROLLBACK] Restoring previous version...");
                    try
                    {
                        CopyDirectory(backupPath, appPath, skipExcluded: false);
                        Console.WriteLine("[OK] Rollback complete.");
                    }
                    catch (Exception rbEx)
                    {
                        Console.WriteLine($"[FATAL] Rollback also failed: {rbEx.Message}");
                        Console.WriteLine("Please reinstall D. Chemist manually.");
                    }
                    Thread.Sleep(6000);
                    return;
                }

                // ── Step 5: Cleanup & Restart ────────────────────────────────
                Console.WriteLine("[5/5] Cleaning up and restarting...");

                try { if (Directory.Exists(backupPath)) Directory.Delete(backupPath, true); }
                catch { /* Ignore cleanup failure */ }

                try { if (File.Exists(zipPath)) File.Delete(zipPath); }
                catch { /* Ignore cleanup failure */ }

                string exePath = Path.Combine(appPath, "DChemist.exe");
                if (File.Exists(exePath))
                {
                    Console.WriteLine($"[OK] Restarting: {exePath}");
                    Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
                }
                else
                {
                    Console.WriteLine($"[ERROR] DChemist.exe not found at: {exePath}");
                    Console.WriteLine("Update was applied but the app could not be restarted. Please start it manually.");
                    Thread.Sleep(5000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\n[FATAL] Unexpected error during update:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("\nPlease contact support if the application fails to start.");
                Thread.Sleep(8000);
            }
        }

        /// <summary>
        /// Extracts zip entries to destination, skipping excluded files and folders.
        /// </summary>
        static void ExtractWithExclusions(string zipPath, string destinationDir)
        {
            using var archive = ZipFile.OpenRead(zipPath);

            foreach (var entry in archive.Entries)
            {
                // Skip directory entries (folders)
                if (string.IsNullOrEmpty(entry.Name)) continue;

                // Check if any path segment is an excluded folder
                var parts = entry.FullName.Split('/', '\\');
                bool inExcludedFolder = false;
                foreach (var part in parts[..^1]) // all except last (filename)
                {
                    if (ExcludedFolders.Contains(part))
                    {
                        inExcludedFolder = true;
                        break;
                    }
                }
                if (inExcludedFolder) continue;

                // Check if the file itself is excluded
                if (ExcludedFiles.Contains(entry.Name)) continue;

                var destPath = Path.Combine(destinationDir, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                var destDir  = Path.GetDirectoryName(destPath)!;
                Directory.CreateDirectory(destDir);

                try
                {
                    entry.ExtractToFile(destPath, overwrite: true);
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[WARN] Could not overwrite {entry.Name}: {ex.Message}");
                }
            }
        }

        static void CopyDirectory(string sourceDir, string destinationDir, bool skipExcluded)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source not found: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                if (skipExcluded && ExcludedFiles.Contains(file.Name)) continue;
                file.CopyTo(Path.Combine(destinationDir, file.Name), overwrite: true);
            }

            foreach (var subDir in dir.GetDirectories())
            {
                if (skipExcluded && ExcludedFolders.Contains(subDir.Name)) continue;
                CopyDirectory(subDir.FullName, Path.Combine(destinationDir, subDir.Name), skipExcluded);
            }
        }
    }
}
