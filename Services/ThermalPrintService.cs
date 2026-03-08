using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Printing;
using Windows.Graphics.Printing;
using WinRT.Interop;

namespace DChemist.Services
{
    public class ThermalPrintService : IPrintService
    {
        private PrintDocument? _printDocument;
        private IPrintDocumentSource? _printDocumentSource;
        private UIElement? _printElement;
        private string _jobName = "Receipt";

        public async Task PrintReceiptAsync(UIElement receiptElement, string jobName)
        {
            _printElement = receiptElement;
            _jobName = jobName;

            // Initialize PrintDocument
            _printDocument = new PrintDocument();
            _printDocumentSource = _printDocument.DocumentSource;

            // Register for PrintDocument events
            _printDocument.Paginate += PrintDocument_Paginate;
            _printDocument.GetPreviewPage += PrintDocument_GetPreviewPage;
            _printDocument.AddPages += PrintDocument_AddPages;

            // Get the window handle
            var window = App.Current.MainWindow;
            if (window == null) return;
            IntPtr hWnd = WindowNative.GetWindowHandle(window);

            // Access the PrintManager for the window
            var printManager = PrintManagerInterop.GetForWindow(hWnd);
            printManager.PrintTaskRequested += PrintManager_PrintTaskRequested;

            // Show the Print UI
            try
            {
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hWnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Printing Error: {ex.Message}");
            }
            finally
            {
                // Unregister events after printing
                printManager.PrintTaskRequested -= PrintManager_PrintTaskRequested;
                _printDocument.Paginate -= PrintDocument_Paginate;
                _printDocument.GetPreviewPage -= PrintDocument_GetPreviewPage;
                _printDocument.AddPages -= PrintDocument_AddPages;
            }
        }

        private void PrintManager_PrintTaskRequested(Windows.Graphics.Printing.PrintManager sender, Windows.Graphics.Printing.PrintTaskRequestedEventArgs args)
        {
            args.Request.CreatePrintTask(_jobName, sourceRequested =>
            {
                sourceRequested.SetSource(_printDocumentSource);
            });
        }

        private void PrintDocument_Paginate(object sender, PaginateEventArgs e)
        {
            // For receipts, we usually have a single tall page
            _printDocument?.SetPreviewPageCount(1, PreviewPageCountType.Final);
        }

        private void PrintDocument_GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            _printDocument?.SetPreviewPage(e.PageNumber, _printElement);
        }

        private void PrintDocument_AddPages(object sender, AddPagesEventArgs e)
        {
            _printDocument?.AddPage(_printElement);
            _printDocument?.AddPagesComplete();
        }
    }

    // Interop helper
    internal static class PrintManagerInterop
    {
        [ComImport]
        [Guid("372F1D3D-1424-4B44-B524-74744419E77F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IPrintManagerInterop
        {
            IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);
            IntPtr ShowPrintUIForWindowAsync([In] IntPtr appWindow, [In] ref Guid riid);
        }

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CharSet = CharSet.Unicode)]
        private static extern int RoGetActivationFactory(string runtimeClassId, ref Guid iid, out IntPtr factory);

        private static IPrintManagerInterop GetInterop()
        {
            Guid iid = new Guid("372F1D3D-1424-4B44-B524-74744419E77F");
            int hr = RoGetActivationFactory("Windows.Graphics.Printing.PrintManager", ref iid, out IntPtr factory);
            if (hr != 0) throw Marshal.GetExceptionForHR(hr) ?? new Exception($"Failed to get activation factory for PrintManager. HRESULT: {hr}");
            return (IPrintManagerInterop)Marshal.GetObjectForIUnknown(factory);
        }

        public static PrintManager GetForWindow(IntPtr hWnd)
        {
            var interop = GetInterop();
            Guid iid = typeof(PrintManager).GUID;
            IntPtr result = interop.GetForWindow(hWnd, ref iid);
            return WinRT.MarshalInspectable<PrintManager>.FromAbi(result);
        }

        public static async Task ShowPrintUIForWindowAsync(IntPtr hWnd)
        {
            var interop = GetInterop();
            Guid iid = new Guid("5AD5CE31-6BC0-4700-9FAD-662174C51305"); // IAsyncAction
            IntPtr result = interop.ShowPrintUIForWindowAsync(hWnd, ref iid);
            var action = WinRT.MarshalInterface<Windows.Foundation.IAsyncAction>.FromAbi(result);
            await action;
        }
    }
}
