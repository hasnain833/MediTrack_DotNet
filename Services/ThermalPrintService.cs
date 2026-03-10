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

        public Task<bool> PrintReceiptSilentAsync(ViewModels.ReceiptViewModel receipt, string printerName)
        {
            return Task.Run(() =>
            {
                try
                {
                    var sb = new System.Text.StringBuilder();
                    // ESC/POS Init
                    sb.Append((char)27).Append((char)64);
                    // Center justify
                    sb.Append((char)27).Append((char)97).Append((char)1);
                    
                    sb.AppendLine(receipt.PharmacyName);
                    sb.AppendLine(receipt.PharmacyAddress);
                    sb.AppendLine(receipt.PharmacyPhone);
                    sb.AppendLine("--------------------------------");
                    
                    // Left justify
                    sb.Append((char)27).Append((char)97).Append((char)0);
                    sb.AppendLine($"Bill No: {receipt.BillNo}");
                    sb.AppendLine($"Date:    {receipt.Date}");
                    sb.AppendLine("--------------------------------");
                    
                    foreach(var item in receipt.Items)
                    {
                        sb.AppendLine($"{item.Name}");
                        sb.AppendLine($"  {item.Quantity} x {item.Price:F2}    = {item.Total:F2}");
                    }
                    sb.AppendLine("--------------------------------");
                    sb.AppendLine($"Subtotal:       {receipt.TotalAmount:F2}");
                    sb.AppendLine($"{receipt.TaxRateText,-16}{receipt.TaxAmount:F2}");
                    if (receipt.DiscountAmount > 0)
                        sb.AppendLine($"Discount:      -{receipt.DiscountAmount:F2}");
                    
                    sb.AppendLine("--------------------------------");
                    sb.AppendLine($"GRAND TOTAL:    {receipt.GrandTotal:F2}");
                    sb.AppendLine("--------------------------------");
                    
                    // Center
                    sb.Append((char)27).Append((char)97).Append((char)1);
                    sb.AppendLine("FBR SIMULATOR MODE");
                    sb.AppendLine(receipt.FbrInvoiceNo);
                    sb.AppendLine("Thank you for your visit!");
                    
                    // Feed and cut
                    sb.Append((char)29).Append((char)86).Append((char)66).Append((char)0);

                    return RawPrinterHelper.SendStringToPrinter(printerName, sb.ToString());
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Silent print failed: {ex.Message}");
                    return false;
                }
            });
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

        [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void RoGetActivationFactory(string runtimeClassId, ref Guid iid, out IntPtr factory);

        private static IPrintManagerInterop GetInterop()
        {
            // Use the IID of IPrintManagerInterop specifically
            Guid iid = new Guid("372F1D3D-1424-4B44-B524-74744419E77F");
            RoGetActivationFactory("Windows.Graphics.Printing.PrintManager", ref iid, out IntPtr factory);
            try
            {
                return (IPrintManagerInterop)Marshal.GetObjectForIUnknown(factory);
            }
            finally
            {
                if (factory != IntPtr.Zero) Marshal.Release(factory);
            }
        }

        public static PrintManager GetForWindow(IntPtr hWnd)
        {
            var interop = GetInterop();
            Guid iid = typeof(PrintManager).GUID;
            IntPtr result = interop.GetForWindow(hWnd, ref iid);
            try
            {
                return WinRT.MarshalInspectable<PrintManager>.FromAbi(result);
            }
            finally
            {
                if (result != IntPtr.Zero) Marshal.Release(result);
            }
        }

        public static async Task ShowPrintUIForWindowAsync(IntPtr hWnd)
        {
            var interop = GetInterop();
            Guid iid = new Guid("5AD5CE31-6BC0-4700-9FAD-662174C51305"); // IAsyncAction
            IntPtr result = interop.ShowPrintUIForWindowAsync(hWnd, ref iid);
            try
            {
                var action = WinRT.MarshalInterface<Windows.Foundation.IAsyncAction>.FromAbi(result);
                await action;
            }
            finally
            {
                if (result != IntPtr.Zero) Marshal.Release(result);
            }
        }
    }

    internal static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName = "";
            [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile = null;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "";
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendStringToPrinter(string szPrinterName, string szString)
        {
            IntPtr pBytes = Marshal.StringToCoTaskMemAnsi(szString);
            bool bSuccess = SendBytesToPrinter(szPrinterName, pBytes, szString.Length);
            Marshal.FreeCoTaskMem(pBytes);
            return bSuccess;
        }

        public static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount)
        {
            int dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false;
            di.pDocName = "D. Chemist Receipt";
            di.pDataType = "RAW";

            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }
            return bSuccess;
        }
    }
}
