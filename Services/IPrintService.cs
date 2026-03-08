using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace DChemist.Services
{
    public interface IPrintService
    {
        Task PrintReceiptAsync(UIElement receiptElement, string jobName);
    }
}
