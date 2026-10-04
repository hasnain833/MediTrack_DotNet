using System;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using DChemist.Services;

namespace DChemist.Views
{
    public sealed partial class UpdateDialog : ContentDialog
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;

        // Shows "Waiting for internet…" when no bytes arrive for a while (the service retries on its own).
        private readonly DispatcherTimer _stallTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private DateTime _lastProgressAt;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            this.InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;

            CurrentVersionText.Text = $"v{updateService.CurrentVersion}";
            VersionText.Text = $"v{updateInfo.LatestVersion}";
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                ? "Performance improvements and bug fixes."
                : updateInfo.ReleaseNotes;

            var logo = Path.Combine(AppContext.BaseDirectory, "Assets", "store-logo.png");
            if (File.Exists(logo)) LogoImage.Source = new BitmapImage(new Uri(logo));

            _stallTimer.Tick += (_, _) =>
            {
                if (DateTime.Now - _lastProgressAt > TimeSpan.FromSeconds(8))
                    StatusText.Text = "Waiting for internet… will continue automatically";
            };

            this.PrimaryButtonClick += UpdateDialog_PrimaryButtonClick;
            this.Closed += (_, _) => _stallTimer.Stop();
        }

        private async void UpdateDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Prevent the dialog from closing immediately
            args.Cancel = true;

            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            HintText.Visibility = Visibility.Collapsed;
            ProgressContainer.Visibility = Visibility.Visible;
            ErrorBar.IsOpen = false;
            StatusText.Text = "Downloading…";
            _lastProgressAt = DateTime.Now;
            _stallTimer.Start();

            try
            {
                var zipPath = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, (progress, done, total) =>
                {
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        _lastProgressAt = DateTime.Now;
                        StatusText.Text = "Downloading…";
                        UpdateProgressBar.Value = progress;
                        PercentText.Text = $"{(int)progress}%";
                        ProgressText.Text = total > 0
                            ? $"{done / 1048576.0:0.0} MB of {total / 1048576.0:0.0} MB"
                            : $"{done / 1048576.0:0.0} MB";
                    });
                }, _updateInfo.PackageSha256);

                _stallTimer.Stop();

                if (zipPath != null)
                {
                    StatusText.Text = "Installing…";
                    PercentText.Text = "100%";
                    UpdateProgressBar.IsIndeterminate = true;
                    ProgressText.Text = "If Windows asks for permission, click Yes. The app will reopen by itself.";

                    bool launched = _updateService.LaunchUpdater(zipPath);
                    if (launched)
                        App.Current.Exit();
                    else
                        ShowError("The update was downloaded, but the installer could not start. Run D. Chemist as Administrator and try again.");
                }
                else
                {
                    ShowError("The download could not finish. Check the internet and press Update now again. It will continue from where it stopped.");
                }
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                ShowError("Windows permission was declined. Press Update now and click Yes on the Windows prompt.");
            }
            catch (Exception ex)
            {
                ShowError($"An error occurred: {ex.Message}");
            }
        }

        private void ShowError(string message)
        {
            this.DispatcherQueue.TryEnqueue(() =>
            {
                _stallTimer.Stop();
                ErrorBar.Message = message;
                ErrorBar.IsOpen = true;
                ProgressContainer.Visibility = Visibility.Collapsed;
                UpdateProgressBar.IsIndeterminate = false;
                PrimaryButtonText = "Try again";
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            });
        }
    }
}
