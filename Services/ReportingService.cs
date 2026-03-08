using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using DChemist.Models;
using DChemist.Repositories;

namespace DChemist.Services
{
    public interface IReportingService
    {
        Task<bool> ExportSalesToCsvAsync(IEnumerable<SaleSummary> sales);
        Task<bool> ExportInventoryToCsvAsync(IEnumerable<Medicine> inventory);
    }

    public class ReportingService : IReportingService
    {
        public async Task<bool> ExportSalesToCsvAsync(IEnumerable<SaleSummary> sales)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Bill No,Customer,Amount,Date,Status");
            foreach (var s in sales)
            {
                sb.AppendLine($"{Escape(s.BillNo)},{Escape(s.Customer)},{s.Amount},{Escape(s.Date)},{Escape(s.Status)}");
            }
            return await SaveFileAsync(sb.ToString(), "Sales_Report_" + DateTime.Now.ToString("yyyyMMdd") + ".csv", ".csv");
        }

        public async Task<bool> ExportInventoryToCsvAsync(IEnumerable<Medicine> inventory)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Generic,Category,Manufacturer,Dosage,Strength,Stock,Selling Price,Purchase Price,Expiry");
            foreach (var m in inventory)
            {
                sb.AppendLine($"{Escape(m.Name)},{Escape(m.GenericName)},{Escape(m.CategoryName)},{Escape(m.ManufacturerName)},{Escape(m.DosageForm)},{Escape(m.Strength)},{m.StockQty},{m.SellingPrice},{m.PurchasePrice},{m.ExpiryDate?.ToString("yyyy-MM-dd")}");
            }
            return await SaveFileAsync(sb.ToString(), "Inventory_Report_" + DateTime.Now.ToString("yyyyMMdd") + ".csv", ".csv");
        }

        private async Task<bool> SaveFileAsync(string content, string fileName, string extension)
        {
            try
            {
                var savePicker = new FileSavePicker();
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                savePicker.FileTypeChoices.Add(extension == ".csv" ? "CSV File" : "Text File", new List<string>() { extension });
                savePicker.SuggestedFileName = fileName;

                // WinUI 3 Window Handle logic
                var window = App.Current.MainWindow;
                IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hwnd);

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file != null)
                {
                    await FileIO.WriteTextAsync(file, content);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Utils.AppLogger.LogError("Failed to save report: " + fileName, ex);
            }
            return false;
        }

        private string Escape(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (text.Contains(",") || text.Contains("\"") || text.Contains("\n"))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }
    }
}
