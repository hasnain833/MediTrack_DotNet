using Microsoft.UI.Xaml.Controls;

namespace DChemist.Views
{
    public sealed partial class ChangePasswordDialog : ContentDialog
    {
        public string NewPassword => NewPasswordField.Password;
        
        public ChangePasswordDialog()
        {
            this.InitializeComponent();
            this.PrimaryButtonClick += OnPrimaryButtonClick;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(NewPasswordField.Password) || NewPasswordField.Password.Length < 6)
            {
                ErrorText.Text = "Password must be at least 6 characters.";
                ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                args.Cancel = true;
                return;
            }

            if (NewPasswordField.Password != ConfirmPasswordField.Password)
            {
                ErrorText.Text = "Passwords do not match.";
                ErrorText.Visibility = Microsoft.UI.Xaml.Visibility.Visible;
                args.Cancel = true;
                return;
            }
        }
    }
}
