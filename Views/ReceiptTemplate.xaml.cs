using Microsoft.UI.Xaml.Controls;
using DChemist.ViewModels;

namespace DChemist.Views
{
    public sealed partial class ReceiptTemplate : UserControl
    {
        public ReceiptViewModel ViewModel { get; }

        public ReceiptTemplate(ReceiptViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
        }
    }
}
