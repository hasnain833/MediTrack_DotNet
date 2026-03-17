using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using DChemist.Models;
using DChemist.ViewModels;

namespace DChemist.Services
{
    public interface IDialogService
    {
        Task<bool> ShowConfirmationAsync(string title, string content, string primaryButtonText, string cancelButtonText);
        Task<Medicine?> ShowInventoryDialogAsync(Medicine? existingMedicine = null);
        Task ShowMessageAsync(string title, string content, string closeButtonText = "OK");
        Task<List<DChemist.Views.ReturnItemModel>?> ShowRefundDialogAsync(Sale sale);
        Task ShowFiscalSettingsDialogAsync();
        Task ShowUpdateDialogAsync(UpdateInfo update, UpdateService updateService);
        Task<string?> ShowChangePasswordDialogAsync();
        Task ShowReceiptPreviewAsync(ReceiptViewModel receipt);
    }

    public class DialogService : IDialogService
    {
        private readonly SettingsService _settings;

        public DialogService(SettingsService settings)
        {
            _settings = settings;
        }

        public async Task ShowReceiptPreviewAsync(ReceiptViewModel receipt)
        {
            if (App.MainRoot?.XamlRoot == null) return;

            var receiptView = new DChemist.Views.ReceiptTemplate(receipt);
            var dialog = new ContentDialog
            {
                Title = "Receipt Preview",
                Content = receiptView,
                CloseButtonText = "Close",
                XamlRoot = App.MainRoot.XamlRoot
            };

            await dialog.ShowAsync();
        }
        public async Task<bool> ShowConfirmationAsync(string title, string content, string primaryButtonText, string cancelButtonText)
        {
            if (App.MainRoot?.XamlRoot == null) return false;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = primaryButtonText,
                CloseButtonText = cancelButtonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainRoot.XamlRoot
            };

            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        public async Task<Medicine?> ShowInventoryDialogAsync(Medicine? existingMedicine = null)
        {
            if (App.MainRoot?.XamlRoot == null) return null;

            DChemist.Views.InventoryDialog dialog;
            if (existingMedicine != null)
                dialog = new DChemist.Views.InventoryDialog(existingMedicine);
            else
                dialog = new DChemist.Views.InventoryDialog();
            
            dialog.XamlRoot = App.MainRoot.XamlRoot;
            var result = await dialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
                return dialog.Result;
                
            return null;
        }

        public async Task ShowMessageAsync(string title, string content, string closeButtonText = "OK")
        {
            if (App.MainRoot?.XamlRoot == null) return;

            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = closeButtonText,
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainRoot.XamlRoot
            };

            await dialog.ShowAsync();
        }

        public async Task<List<DChemist.Views.ReturnItemModel>?> ShowRefundDialogAsync(Sale sale)
        {
            if (App.MainRoot?.XamlRoot == null) return null;

            var dialog = new DChemist.Views.RefundDialog(sale)
            {
                XamlRoot = App.MainRoot.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
                return dialog.Result;

            return null;
        }

        public async Task ShowFiscalSettingsDialogAsync()
        {
            if (App.MainRoot?.XamlRoot == null) return;

            var dialog = new DChemist.Views.FiscalSettingsDialog(_settings)
            {
                XamlRoot = App.MainRoot.XamlRoot
            };

            await dialog.ShowAsync();
        }

        public async Task ShowUpdateDialogAsync(UpdateInfo update, UpdateService updateService)
        {
            if (App.MainRoot?.XamlRoot == null) return;

            var dialog = new DChemist.Views.UpdateDialog(update, updateService)
            {
                    XamlRoot = App.MainRoot.XamlRoot
                };
    
                await dialog.ShowAsync();
            }
    
            public async Task<string?> ShowChangePasswordDialogAsync()
            {
                if (App.MainRoot?.XamlRoot == null) return null;
    
                var dialog = new DChemist.Views.ChangePasswordDialog
                {
                    XamlRoot = App.MainRoot.XamlRoot
                };
    
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    return dialog.NewPassword;
    
                return null;
            }
        }
    }
