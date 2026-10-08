using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using DChemist.Services;
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
        private async void OnSectionClick(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton nav) return;
            foreach (var b in new[] { NavPharmacy, NavPrinter, NavPreview, NavBackup, NavUpdates, NavDanger })
                b.IsChecked = b == nav;
            foreach (var s in new FrameworkElement[] { SecPharmacy, SecPrinter, SecPreview, SecBackup, SecUpdates, SecDanger })
                s.Visibility = s.Name == (string)nav.Tag ? Visibility.Visible : Visibility.Collapsed;
            if (nav == NavPreview) await RenderPreviewAsync();
        }

        // Builds a sample bill the same way SalesWorkflowService does, then renders both print paths.
        private async System.Threading.Tasks.Task RenderPreviewAsync()
        {
            var receipt = new ReceiptViewModel
            {
                BillNo = "SAMPLE-0001",
                CustomerName = "Walk-in Customer",
                TotalAmount = 1130, TaxAmount = 0, TaxRateText = "Tax (0%):", DiscountAmount = 50, GrandTotal = 1080
            };
            receipt.Items.Add(new ReceiptItemViewModel { Name = "Panadol 500mg Tab", Quantity = 2, Price = 45 });
            receipt.Items.Add(new ReceiptItemViewModel { Name = "Augmentin 625mg Tablets (long name)", Quantity = 1, Price = 640 });
            receipt.Items.Add(new ReceiptItemViewModel { Name = "Brufen Syrup", Quantity = 2, Price = 200 });
            receipt.CashReceived = 1100;
            await receipt.LoadStoreDetailsAsync(App.Current.Services.GetRequiredService<SettingsService>());
            // Show what is on screen now, saved or not
            receipt.PharmacyName = ViewModel.PharmacyName;
            receipt.PharmacyAddress = ViewModel.PharmacyAddress;
            receipt.PharmacyPhone = ViewModel.PharmacyPhone;
            receipt.PharmacyLicense = ViewModel.PharmacyLicense;
            receipt.PharmacyNtn = ViewModel.PharmacyNtn;
            receipt.ReceiptFooter = ViewModel.ReceiptFooter;

            DialogPreview.Content = new ReceiptTemplate(receipt);
            RenderEscPos(receipt);
        }

        private async void OnFooterChanged(object sender, TextChangedEventArgs e)
        {
            if (SecPreview.Visibility == Visibility.Visible) await RenderPreviewAsync();
        }

        // Interprets the ESC/POS codes ReceiptBuilder emits (align, size, cut) into on-screen lines.
        private void RenderEscPos(ReceiptViewModel receipt)
        {
            ThermalPreview.Children.Clear();
            if (receipt.StoreLogo != null)
                ThermalPreview.Children.Add(new Image { Source = receipt.StoreLogo, Height = 48, HorizontalAlignment = HorizontalAlignment.Center });

            string data = ReceiptBuilder.BuildReceiptString(receipt);
            var align = TextAlignment.Left;
            bool large = false;
            var line = new System.Text.StringBuilder();

            void Flush()
            {
                ThermalPreview.Children.Add(new TextBlock
                {
                    Text = line.Length == 0 ? " " : line.ToString(),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = large ? 24 : 12,
                    FontWeight = large ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
                    TextAlignment = align,
                    TextWrapping = TextWrapping.Wrap
                });
                line.Clear();
            }

            for (int i = 0; i < data.Length; i++)
            {
                char c = data[i];
                if (c == (char)27 && i + 2 < data.Length)
                {
                    if (data[i + 1] == 'a') align = data[i + 2] == (char)1 ? TextAlignment.Center : data[i + 2] == (char)2 ? TextAlignment.Right : TextAlignment.Left;
                    else if (data[i + 1] == '!') large = (data[i + 2] & 0x30) != 0;
                    i += 2;
                }
                else if (c == (char)29) i += 3; // GS V cut
                else if (c == '\n') Flush();
                else if (c != '\r') line.Append(c);
            }
            if (line.Length > 0) Flush();
        }
    }
}
