using Microsoft.UI.Xaml.Controls;
using MediTrack.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace MediTrack.Views
{
    public sealed partial class UserManagementPage : Page
    {
        public UserManagementViewModel ViewModel { get; }

        public UserManagementPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<UserManagementViewModel>();
        }
    }
}
