using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using DChemist.Services;

namespace DChemist.Views
{
    public sealed partial class UpdateDialog : ContentDialog
    {
        private readonly UpdateInfo _updateInfo;
        private readonly UpdateService _updateService;

        public UpdateDialog(UpdateInfo updateInfo, UpdateService updateService)
        {
            this.InitializeComponent();
            _updateInfo = updateInfo;
            _updateService = updateService;

            VersionText.Text = $"Version {updateInfo.LatestVersion}";
            ReleaseNotesText.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) 
                ? "Performance improvements and bug fixes." 
                : updateInfo.ReleaseNotes;

            this.PrimaryButtonClick += UpdateDialog_PrimaryButtonClick;
        }

        private async void UpdateDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Prevent the dialog from closing immediately
            args.Cancel = true;

            // Update UI to show progress
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            ProgressContainer.Visibility = Visibility.Visible;
            ErrorText.Visibility = Visibility.Collapsed;

            try
            {
                var zipPath = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, progress =>
                {
                    // Update progress bar on UI thread
                    this.DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateProgressBar.Value = progress;
                        ProgressText.Text = $"{(int)progress}%";
                    });
                });

                if (zipPath != null)
                {
                    // Success! Launch updater and exit
                    _updateService.LaunchUpdater(zipPath);
                    App.Current.Exit();
                }
                else
                {
                    ShowError("Failed to download update. Please check your internet connection and try again.");
                }
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
                ErrorText.Text = message;
                ErrorText.Visibility = Visibility.Visible;
                ProgressContainer.Visibility = Visibility.Collapsed;
                IsPrimaryButtonEnabled = true;
                IsSecondaryButtonEnabled = true;
            });
        }
    }
}
