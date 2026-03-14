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
            if (printManager == null)
            {
                throw new InvalidOperationException("PrintManager could not be initialized. Ensure a printer is available.");
            }

            printManager.PrintTaskRequested += PrintManager_PrintTaskRequested;

            // Show the Print UI
            try
            {
                await PrintManagerInterop.ShowPrintUIForWindowAsync(hWnd);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Printing Error: {ex.Message}");
                throw; // Rethrow so the caller knows it failed
            }
            finally
            {
                // Unregister events after printing
                printManager.PrintTaskRequested -= PrintManager_PrintTaskRequested;
                
                if (_printDocument != null)
                {
                    _printDocument.Paginate -= PrintDocument_Paginate;
                    _printDocument.GetPreviewPage -= PrintDocument_GetPreviewPage;
                    _printDocument.AddPages -= PrintDocument_AddPages;
                }
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
            [PreserveSig]
            int GetForWindow([In] IntPtr appWindow, [In] ref Guid riid, out IntPtr result);
            [PreserveSig]
            int ShowPrintUIForWindowAsync([In] IntPtr appWindow, [In] ref Guid riid, out IntPtr result);
        }

        [DllImport("combase.dll", SetLastError = true, PreserveSig = true)]
        private static extern int RoGetActivationFactory(IntPtr runtimeClassId, ref Guid iid, out IntPtr factory);

        [DllImport("combase.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int WindowsCreateString(string sourceString, uint length, out IntPtr hstring);

        [DllImport("combase.dll", PreserveSig = true)]
        private static extern int WindowsDeleteString(IntPtr hstring);

        private static IPrintManagerInterop? GetInterop()
        {
            IntPtr hClassName = IntPtr.Zero;
            IntPtr factory = IntPtr.Zero;
            try
            {
                string className = "Windows.Graphics.Printing.PrintManager";
                WindowsCreateString(className, (uint)className.Length, out hClassName);
                
                Guid iid = new Guid("372F1D3D-1424-4B44-B524-74744419E77F"); 
                int hr = RoGetActivationFactory(hClassName, ref iid, out factory);
                
                if (hr != 0 || factory == IntPtr.Zero)
                {
                    System.Diagnostics.Debug.WriteLine($"[PrintManagerInterop] Failed to get factory. HRESULT: 0x{hr:X}");
                    return null;
                }

                return (IPrintManagerInterop)Marshal.GetObjectForIUnknown(factory);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintManagerInterop] Exception in GetInterop: {ex.Message}");
                return null;
            }
            finally
            {
                if (hClassName != IntPtr.Zero) WindowsDeleteString(hClassName);
                if (factory != IntPtr.Zero) Marshal.Release(factory);
            }
        }

        public static PrintManager? GetForWindow(IntPtr hWnd)
        {
            try
            {
                var interop = GetInterop();
                if (interop == null) return null;

                Guid iid = typeof(PrintManager).GUID;
                int hr = interop.GetForWindow(hWnd, ref iid, out IntPtr result);
                
                if (hr != 0 || result == IntPtr.Zero) return null;

                try
                {
                    return WinRT.MarshalInspectable<PrintManager>.FromAbi(result);
                }
                finally
                {
                    Marshal.Release(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintManagerInterop] GetForWindow failed: {ex.Message}");
                return null;
            }
        }

        public static async Task ShowPrintUIForWindowAsync(IntPtr hWnd)
        {
            try
            {
                var interop = GetInterop();
                if (interop == null) return;

                Guid iid = new Guid("5AD5CE31-6BC0-4700-9FAD-662174C51305"); // IAsyncAction
                int hr = interop.ShowPrintUIForWindowAsync(hWnd, ref iid, out IntPtr result);
                
                if (hr != 0 || result == IntPtr.Zero) return;

                try
                {
                    var action = WinRT.MarshalInterface<Windows.Foundation.IAsyncAction>.FromAbi(result);
                    await action;
                }
                finally
                {
                    Marshal.Release(result);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[PrintManagerInterop] ShowPrintUIForWindowAsync failed: {ex.Message}");
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
