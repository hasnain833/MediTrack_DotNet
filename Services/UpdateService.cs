using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
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
                Timeout = TimeSpan.FromSeconds(15)
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
        public async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<double> progressCallback)
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

                using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                await using var contentStream = await response.Content.ReadAsStreamAsync();
                await using var fileStream    = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer    = new byte[8192];
                var totalRead = 0L;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    await fileStream.WriteAsync(buffer, 0, read);
                    totalRead += read;

                    if (totalBytes > 0)
                        progressCallback((double)totalRead / totalBytes * 100);
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
        public void LaunchUpdater(string zipPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath))
                {
                    AppLogger.LogError($"UpdateService: Update file not found: {zipPath}");
                    return;
                }

                var appPath     = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                var updaterPath = Path.Combine(appPath, "updater.exe");

                if (!File.Exists(updaterPath))
                {
                    AppLogger.LogError("UpdateService: updater.exe not found — cannot apply update.");
                    return;
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

                System.Diagnostics.Process.Start(startInfo);
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
            }
        }
    }
}
