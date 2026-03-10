using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using DChemist.Services;

namespace DChemist.Views
{
    public sealed partial class FiscalSettingsDialog : ContentDialog
    {
        private readonly SettingsService _settings;

        public FiscalSettingsDialog(SettingsService settings)
        {
            this.InitializeComponent();
            _settings = settings;
            _ = LoadSettingsAsync();

            this.PrimaryButtonClick += async (s, e) =>
            {
                var deferral = e.GetDeferral();
                try
                {
                    await _settings.SaveSettingAsync("fbr_is_live", IsLiveToggle.IsOn.ToString().ToLower());
                    await _settings.SaveSettingAsync("fbr_pos_id", PosIdInput.Text);
                    await _settings.SaveSettingAsync("fbr_api_url", ApiUrlInput.Text);
                    await _settings.SaveSettingAsync("fbr_token", TokenInput.Password);
                }
                finally
                {
                    deferral.Complete();
                }
            };
        }

        private async Task LoadSettingsAsync()
        {
            IsLiveToggle.IsOn = (await _settings.GetSettingAsync("fbr_is_live", "false")).ToLower() == "true";
            PosIdInput.Text = await _settings.GetSettingAsync("fbr_pos_id", "");
            ApiUrlInput.Text = await _settings.GetSettingAsync("fbr_api_url", "");
            TokenInput.Password = await _settings.GetSettingAsync("fbr_token", "");
        }
    }
}
