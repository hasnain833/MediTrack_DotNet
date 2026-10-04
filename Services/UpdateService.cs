using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Utils;

namespace DChemist.Services
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl   { get; set; } = string.Empty;
        public string ReleaseNotes  { get; set; } = string.Empty;
        public string? PackageSha256 { get; set; }
    }

    public class UpdateService
    {
        private readonly IConfiguration _configuration;
        private readonly AuthorizationService _authService;
        private readonly HttpClient _httpClient;
        private readonly string _versionJsonUrl;

        // ── Read version from assembly so it's always accurate ──────────────
        public string CurrentVersion { get; }

        public UpdateService(IConfiguration configuration, AuthorizationService authService)
        {
            _configuration = configuration;
            _authService   = authService;

            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromMinutes(60)
            };

            // Assembly version (set in .csproj → <Version>)
            var asmVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            CurrentVersion = asmVersion != null
                ? $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}.{asmVersion.Revision}"
                : (_configuration["Update:CurrentVersion"] ?? "1.0.0.0");

            // Full URL to version.json — fall back to base URL + filename
            var baseUrl = _configuration["Update:UpdateServerUrl"] ?? string.Empty;
            _versionJsonUrl = _configuration["Update:VersionJsonUrl"]
                              ?? (baseUrl.TrimEnd('/') + "/version.json");
        }

        /// <summary>
        /// Checks GitHub for a newer version. Returns UpdateInfo if one is found,
        /// or null if up-to-date / network unavailable. Never throws.
        /// </summary>
        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (string.IsNullOrEmpty(_versionJsonUrl))
            {
                AppLogger.LogWarning("UpdateService: UpdateServerUrl is not configured.");
                return null;
            }

            try
            {
                AppLogger.LogInfo($"UpdateService: Checking for updates at {_versionJsonUrl}");
                using var response = await _httpClient.GetAsync(_versionJsonUrl);

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    AppLogger.LogWarning("UpdateService: version.json not found (404).");
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();

                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (updateInfo == null)
                {
                    AppLogger.LogWarning("UpdateService: version.json deserialized to null.");
                    return null;
                }

                if (IsNewerVersion(updateInfo.LatestVersion))
                {
                    AppLogger.LogInfo($"UpdateService: Update available — {CurrentVersion} → {updateInfo.LatestVersion}");
                    return updateInfo;
                }

                AppLogger.LogInfo($"UpdateService: App is up-to-date (v{CurrentVersion}).");
                return null;
            }
            catch (TaskCanceledException)
            {
                AppLogger.LogWarning("UpdateService: Update check timed out.");
                return null;
            }
            catch (HttpRequestException ex)
            {
                AppLogger.LogWarning($"UpdateService: Could not reach update server: {ex.Message}");
                return null;
            }
            catch (JsonException ex)
            {
                AppLogger.LogError("UpdateService: Invalid version.json format", ex);
                return null;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService: Unexpected error during update check", ex);
                return null;
            }
        }

        private bool IsNewerVersion(string latestVersion)
        {
            if (Version.TryParse(latestVersion, out var latest) &&
                Version.TryParse(CurrentVersion,  out var current))
            {
                return latest > current;
            }
            return false;
        }

        /// <summary>
        /// Downloads the update zip to a local temp folder with progress reporting.
        /// Returns the local zip path on success, or null on failure.
        /// </summary>
        public async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<double, long, long> progressCallback, string? expectedSha256 = null)
        {
            try
            {
                var updateDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "D. Chemist", "Updates");
                Directory.CreateDirectory(updateDir);

                var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName)) fileName = "update.zip";

                var filePath = Path.Combine(updateDir, fileName);

                AppLogger.LogInfo($"UpdateService: Downloading update from {downloadUrl}");

                // Weak shop connections drop mid-download. HttpClient.Timeout doesn't cover body reads,
                // so a dead connection used to hang forever. Now: a read stalled for 30 s aborts the attempt,
                // and the next attempt resumes from the bytes already on disk (HTTP Range).
                const int maxAttempts = 8;
                for (int attempt = 1; ; attempt++)
                {
                    try
                    {
                        await DownloadAttemptAsync(downloadUrl, filePath, progressCallback);
                        break;
                    }
                    catch (Exception ex) when (attempt < maxAttempts && ex is IOException or HttpRequestException or OperationCanceledException)
                    {
                        AppLogger.LogWarning($"UpdateService: Download attempt {attempt} interrupted ({ex.Message}); resuming in 3 s.");
                        await Task.Delay(3000);
                    }
                }

                if (!string.IsNullOrWhiteSpace(expectedSha256))
                {
                    var actualSha256 = await Task.Run(() => ComputeFileSha256(filePath)); // 70+ MB: keep UI responsive
                    if (!actualSha256.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(filePath); } catch { /* best effort cleanup */ }
                        throw new InvalidDataException("Update package integrity verification failed (SHA-256 mismatch).");
                    }
                }

                AppLogger.LogInfo($"UpdateService: Download complete → {filePath}");
                return filePath;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService: Download failed", ex);
                return null;
            }
        }

        /// <summary>
        /// Copies updater.exe to a safe location outside the app folder, then launches
        /// it from there so it is never locked when the update tries to replace files
        /// inside the app directory (including updater.exe itself).
        /// </summary>
        public bool LaunchUpdater(string zipPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                {
                    AppLogger.LogError($"UpdateService: Update file not found: {zipPath}");
                    return false;
                }

                var appPath     = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                var updaterPath = Path.Combine(appPath, "updater.exe");

                if (!File.Exists(updaterPath))
                {
                    AppLogger.LogError("UpdateService: updater.exe not found — cannot apply update.");
                    return false;
                }

                // ── KEY FIX: Copy updater to %LocalAppData%\D. Chemist\ ───────────────
                // Running the updater from inside the app folder causes Windows to lock
                // updater.exe, which then fails when the update tries to replace it.
                // By running from LocalAppData the app folder is never held open by us.
                var safeDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "D. Chemist");
                Directory.CreateDirectory(safeDir);

                var safeUpdaterPath = Path.Combine(safeDir, "updater.exe");

                // Always refresh the copy so it matches the shipped version
                File.Copy(updaterPath, safeUpdaterPath, overwrite: true);

                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;

                AppLogger.LogInfo(
                    $"UpdateService: Launching updater from safe location (PID: {processId}) → {safeUpdaterPath}");

                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName         = safeUpdaterPath,
                    Arguments        = $"\"{appPath}\" \"{zipPath}\" {processId}",
                    UseShellExecute  = true,
                    Verb             = "runas"   // Request admin elevation via UAC
                };

                var started = System.Diagnostics.Process.Start(startInfo);
                if (started == null)
                {
                    AppLogger.LogError("UpdateService: Process.Start returned null; updater did not launch.");
                    return false;
                }
                return true;
            }
            catch (System.ComponentModel.Win32Exception win32ex)
                when (win32ex.NativeErrorCode == 1223) // ERROR_CANCELLED — user clicked "No" on UAC
            {
                AppLogger.LogWarning("UpdateService: UAC elevation was cancelled by the user.");
                throw; // Let the caller display a friendly message
            }
            catch (Exception ex)
            {
                AppLogger.LogError("UpdateService: Failed to launch updater", ex);
                return false;
            }
        }

        /// <summary>One download try; appends to an existing partial file when the server supports Range.</summary>
        private async Task DownloadAttemptAsync(string downloadUrl, string filePath, Action<double, long, long> progressCallback)
        {
            long existing = File.Exists(filePath) ? new FileInfo(filePath).Length : 0;

            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            if (existing > 0)
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(existing, null);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            // 416 = we already have the whole file (e.g. a previous run finished but hash/launch failed).
            if (response.StatusCode == System.Net.HttpStatusCode.RequestedRangeNotSatisfiable) return;
            response.EnsureSuccessStatusCode();

            // Server ignored Range (200 instead of 206) → start over.
            bool resuming = existing > 0 && response.StatusCode == System.Net.HttpStatusCode.PartialContent;
            if (!resuming) existing = 0;

            long totalBytes = response.Content.Headers.ContentLength is long len ? len + existing : -1L;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(filePath, resuming ? FileMode.Append : FileMode.Create,
                                                        FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            long totalRead = existing;
            int lastPercent = -1;
            using var stall = new System.Threading.CancellationTokenSource();

            while (true)
            {
                stall.CancelAfter(TimeSpan.FromSeconds(30));
                int read = await contentStream.ReadAsync(buffer.AsMemory(), stall.Token);
                if (read == 0) break;
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                totalRead += read;

                int percent = totalBytes > 0 ? (int)(totalRead * 100 / totalBytes) : 0;
                if (percent != lastPercent || totalRead % (1024 * 1024) < read)
                {
                    lastPercent = percent;
                    progressCallback(percent, totalRead, totalBytes);
                }
            }

            if (totalBytes > 0 && totalRead < totalBytes)
                throw new IOException($"Connection closed early ({totalRead} of {totalBytes} bytes).");
        }

        private static string ComputeFileSha256(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            using var sha = SHA256.Create();
            var hashBytes = sha.ComputeHash(stream);
            return Convert.ToHexString(hashBytes);
        }
    }
}
