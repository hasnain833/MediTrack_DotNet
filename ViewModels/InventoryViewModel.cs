using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using MediTrack.Models;
using MediTrack.Repositories;
using MediTrack.Services;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly MedicineRepository _medicineRepository;
        private readonly AuthorizationService _auth;
        private string _searchText = string.Empty;
        private bool _isBusy;

        public InventoryViewModel(MedicineRepository medicineRepository, AuthorizationService auth)
        {
            _medicineRepository = medicineRepository;
            _auth = auth;
            Medicines = new ObservableCollection<Medicine>();
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddMedicineCommand = new RelayCommand(async _ => await ExecuteAddMedicineAsync());
            EditMedicineCommand = new RelayCommand(async m => await ExecuteEditMedicineAsync(m as Medicine));
            DeleteMedicineCommand = new RelayCommand(async m => await ExecuteDeleteMedicineAsync(m as Medicine));
            
            _ = RefreshAsync();
        }

        public ObservableCollection<Medicine> Medicines { get; }
        public bool IsAdmin => _auth.IsAdmin;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _ = SearchAsync();
                }
            }
        }

        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        public ICommand RefreshCommand { get; }
        public ICommand AddMedicineCommand { get; }
        public ICommand EditMedicineCommand { get; }
        public ICommand DeleteMedicineCommand { get; }

        private async Task RefreshAsync()
        {
            IsBusy = true;
            try
            {
                var list = await _medicineRepository.GetAllAsync();
                Medicines.Clear();
                foreach (var item in list) Medicines.Add(item);
            }
            finally { IsBusy = false; }
        }

        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                await RefreshAsync();
                return;
            }

            IsBusy = true;
            try
            {
                var list = await _medicineRepository.SearchAsync(SearchText);
                Medicines.Clear();
                foreach (var item in list) Medicines.Add(item);
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteAddMedicineAsync()
        {
            var dialog = new MediTrack.Views.InventoryDialog();
            dialog.XamlRoot = App.MainRoot.XamlRoot;
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await _medicineRepository.AddAsync(dialog.Result);
                await RefreshAsync();
            }
        }

        private async Task ExecuteEditMedicineAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            var dialog = new MediTrack.Views.InventoryDialog(medicine);
            dialog.XamlRoot = App.MainRoot.XamlRoot;
            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await _medicineRepository.UpdateAsync(dialog.Result);
                await RefreshAsync();
            }
        }

        private async Task ExecuteDeleteMedicineAsync(Medicine? medicine)
        {
            if (medicine == null) return;
            
            var dialog = new ContentDialog
            {
                Title = "Delete Medicine",
                Content = $"Are you sure you want to delete {medicine.MedicineName}?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = App.MainRoot.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await _medicineRepository.DeleteAsync(medicine.Id);
                await RefreshAsync();
            }
        }
    }
}
