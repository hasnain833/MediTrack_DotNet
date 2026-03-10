using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using DChemist.Utils;

namespace DChemist.Services
{
    public class UpdateInfo
    {
        public string LatestVersion { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string ReleaseNotes { get; set; } = string.Empty;
    }

    public class UpdateService
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;
        private readonly string _currentVersion;
        private readonly string _updateServerUrl;

        public UpdateService(IConfiguration configuration)
        {
            _configuration = configuration;
            _httpClient = new HttpClient();
            _currentVersion = _configuration["Update:CurrentVersion"] ?? "1.0.0";
            _updateServerUrl = _configuration["Update:UpdateServerUrl"] ?? string.Empty;
        }

        public string CurrentVersion => _currentVersion;

        public async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            if (string.IsNullOrEmpty(_updateServerUrl)) return null;

            try
            {
                using var response = await _httpClient.GetAsync(_updateServerUrl + "version.json");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Update server not yet ready/configured - fail silently or log a warning
                    AppLogger.LogWarning($"Update server returned 404. Checking URL: {_updateServerUrl}version.json");
                    return null;
                }

                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (updateInfo != null && IsNewerVersion(updateInfo.LatestVersion))
                {
                    return updateInfo;
                }
            }
            catch (HttpRequestException ex)
            {
                AppLogger.LogWarning($"Could not reach update server: {ex.Message}");
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to check for updates", ex);
            }

            return null;
        }

        private bool IsNewerVersion(string latestVersion)
        {
            if (Version.TryParse(latestVersion, out var latest) && Version.TryParse(_currentVersion, out var current))
            {
                return latest > current;
            }
            return false;
        }

        public async Task<string?> DownloadUpdateAsync(string downloadUrl, Action<double> progressCallback)
        {
            try
            {
                var updateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "D. Chemist", "Updates");
                Directory.CreateDirectory(updateDir);

                var fileName = Path.GetFileName(new Uri(downloadUrl).LocalPath);
                if (string.IsNullOrEmpty(fileName)) fileName = "update.zip";
                
                var filePath = Path.Combine(updateDir, fileName);

                using (var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        var totalRead = 0L;
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            if (totalBytes != -1)
                            {
                                progressCallback((double)totalRead / totalBytes * 100);
                            }
                        }
                    }
                }

                return filePath;
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to download update", ex);
                return null;
            }
        }

        public void LaunchUpdater(string zipPath)
        {
            try
            {
                var appPath = AppDomain.CurrentDomain.BaseDirectory;
                var updaterPath = Path.Combine(appPath, "updater.exe");
                
                if (!File.Exists(updaterPath))
                {
                    AppLogger.LogError("Updater.exe not found.");
                    return;
                }

                var processId = System.Diagnostics.Process.GetCurrentProcess().Id;
                
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = updaterPath,
                    Arguments = $"\"{appPath}\" \"{zipPath}\" {processId}",
                    UseShellExecute = true,
                    Verb = "runas" // Request elevation
                };

                System.Diagnostics.Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                AppLogger.LogError("Failed to launch updater", ex);
            }
        }
    }
}
