using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using DChemist.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace DChemist.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<SettingsViewModel>();
        }

        // Left nav: show one section at a time.
        private void OnSectionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton nav) return;
            foreach (var b in new[] { NavPharmacy, NavPrinter, NavBackup, NavUpdates, NavDanger })
                b.IsChecked = b == nav;
            foreach (var s in new FrameworkElement[] { SecPharmacy, SecPrinter, SecBackup, SecUpdates, SecDanger })
                s.Visibility = s.Name == (string)nav.Tag ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
