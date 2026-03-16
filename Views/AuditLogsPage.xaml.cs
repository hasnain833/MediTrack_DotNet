using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using DChemist.ViewModels;

namespace DChemist.Views
{
    public sealed partial class AuditLogsPage : Page
    {
        public AuditLogsViewModel ViewModel { get; }

        public AuditLogsPage()
        {
            this.InitializeComponent();
            ViewModel = App.Current.Services.GetRequiredService<AuditLogsViewModel>();
        }
    }
}
