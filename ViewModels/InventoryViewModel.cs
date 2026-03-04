using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using MediTrack.Models;
using MediTrack.Repositories;
using MediTrack.Utils;

namespace MediTrack.ViewModels
{
    public class InventoryViewModel : ViewModelBase
    {
        private readonly MedicineRepository _medicineRepository;
        private string _searchText = string.Empty;
        private bool _isBusy;

        public InventoryViewModel(MedicineRepository medicineRepository)
        {
            _medicineRepository = medicineRepository;
            Medicines = new ObservableCollection<Medicine>();
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            
            _ = RefreshAsync();
        }

        public ObservableCollection<Medicine> Medicines { get; }

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
    }
}
