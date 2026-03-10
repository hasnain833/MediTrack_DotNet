using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DChemistUpdater
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine(" D. Chemist - Automatic Update System ");
            Console.WriteLine("-----------------------------------------");

            if (args.Length < 3)
            {
                Console.WriteLine("Error: Missing arguments.");
                Console.WriteLine("Usage: updater.exe <appPath> <zipPath> <processId>");
                Thread.Sleep(3000);
                return;
            }

            string appPath = args[0];
            string zipPath = args[1];
            int processId = int.Parse(args[2]);

            try
            {
                // 1. Wait for the main application to close
                Console.WriteLine($"Waiting for D. Chemist (PID: {processId}) to exit...");
                try
                {
                    var process = Process.GetProcessById(processId);
                    
                    // SECURITY: Verify the process name to prevent spoofing
                    string procName = process.ProcessName.ToLower();
                    if (!procName.Contains("dchemist") && !procName.Contains("meditrack"))
                    {
                        Console.WriteLine($"Security Error: PID {processId} ({procName}) is not authorized for update.");
                        return;
                    }

                    if (!process.WaitForExit(10000))
                    {
                        Console.WriteLine("Main application is taking too long to close. Killing it...");
                        process.Kill();
                    }
                }
                catch (ArgumentException)
                {
                    // Process already exited
                }

                Console.WriteLine("Application closed.");
                Thread.Sleep(1000); // Small delay to release file locks

                // 2. Create Backup
                string backupPath = appPath.TrimEnd(Path.DirectorySeparatorChar) + "_backup";
                Console.WriteLine($"Creating backup at: {backupPath}...");
                
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, true);

                CopyDirectory(appPath, backupPath);

                // 3. Extract Update
                Console.WriteLine("Applying updates...");
                try
                {
                    // SECURITY: Basic manifest validation before extraction
                    using (ZipArchive archive = ZipFile.OpenRead(zipPath))
                    {
                        bool valid = archive.Entries.Any(e => e.FullName.EndsWith("DChemist.dll", StringComparison.OrdinalIgnoreCase));
                        if (!valid)
                        {
                            Console.WriteLine("Security Error: Update package is invalid or malicious (missing DChemist.dll).");
                            return;
                        }
                    }

                    ZipFile.ExtractToDirectory(zipPath, appPath, true);
                    Console.WriteLine("Update applied successfully.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Update failed: {ex.Message}");
                    Console.WriteLine("Rolling back to previous version...");
                    
                    // Rollback
                    if (Directory.Exists(backupPath))
                    {
                        Directory.Delete(appPath, true);
                        CopyDirectory(backupPath, appPath);
                        Console.WriteLine("Rollback complete.");
                    }
                    throw;
                }

                // 4. Cleanup and Restart
                Console.WriteLine("Cleaning up...");
                if (Directory.Exists(backupPath))
                    Directory.Delete(backupPath, true);
                
                if (File.Exists(zipPath))
                    File.Delete(zipPath);

                Console.WriteLine("Restarting D. Chemist...");
                string exePath = Path.Combine(appPath, "DChemist.exe");
                
                if (File.Exists(exePath))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    Console.WriteLine($"Error: {exePath} not found after update!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FATAL ERROR during update:");
                Console.WriteLine(ex.ToString());
                Console.WriteLine("\nPlease contact support if the application fails to start.");
                Thread.Sleep(5000);
            }
        }

        static void CopyDirectory(string sourceDir, string destinationDir)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists) throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (FileInfo file in dir.GetFiles())
            {
                // Don't copy the updater itself or the backup folder if it's nested (it shouldn't be)
                if (file.Name.Equals("updater.exe", StringComparison.OrdinalIgnoreCase)) continue;
                
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            foreach (DirectoryInfo subDir in dir.GetDirectories())
            {
                string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                CopyDirectory(subDir.FullName, newDestinationDir);
            }
        }
    }
}
