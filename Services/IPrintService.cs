using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DChemist.Services
{
    public interface IPrintService
    {
        Task PrintReceiptAsync(UIElement receiptElement, string jobName);
        /// <summary>Windows print dialog for a multi-page document (one element per page).</summary>
        Task PrintPagesAsync(System.Collections.Generic.IReadOnlyList<UIElement> pages, string jobName);
        Task<bool> PrintReceiptSilentAsync(DChemist.ViewModels.ReceiptViewModel receipt, string printerName);
    }
}
