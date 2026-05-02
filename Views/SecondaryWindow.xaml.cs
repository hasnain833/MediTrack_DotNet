using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DChemist.Views
{
    public sealed partial class SecondaryWindow : Window
    {
        public Frame Frame => SecondaryFrame;

        public SecondaryWindow()
        {
            this.InitializeComponent();
        }
    }
}
