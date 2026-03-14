using System;
using System.Collections.Generic;
using DChemist.Utils;

namespace DChemist.Models
{
    public class ReceivingItem : ViewModelBase
    {
        public int    MedicineId       { get; set; }
        public string MedicineName     { get; set; } = string.Empty;
        public string ManufacturerName { get; set; } = string.Empty;
        private string _batchNo = string.Empty;
        public string BatchNo
        {
            get => _batchNo;
            set => SetProperty(ref _batchNo, value);
        }

        private DateTime? _expiryDate;
        public DateTime? ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        public int    SupplierId   { get; set; }
        public string SupplierName { get; set; } = string.Empty;

        private string _invoiceNo = string.Empty;
        public string InvoiceNo
        {
            get => _invoiceNo;
            set => SetProperty(ref _invoiceNo, value);
        }

        private DateTime? _invoiceDate;
        public DateTime? InvoiceDate
        {
            get => _invoiceDate;
            set => SetProperty(ref _invoiceDate, value);
        }

        private int _quantityUnits = 1;
        public int QuantityUnits
        {
            get => _quantityUnits;
            set
            {
                if (SetProperty(ref _quantityUnits, value))
                {
                    OnPropertyChanged(nameof(UnitCost));
                    OnPropertyChanged(nameof(SellingPricePerUnit));
                }
            }
        }

        private decimal _purchaseTotalPrice;
        public decimal PurchaseTotalPrice
        {
            get => _purchaseTotalPrice;
            set 
            {
                if (SetProperty(ref _purchaseTotalPrice, value))
                    OnPropertyChanged(nameof(UnitCost));
            }
        }

        public decimal UnitCost => QuantityUnits > 0 ? PurchaseTotalPrice / QuantityUnits : 0;

        private decimal _totalSellingPrice;
        public decimal TotalSellingPrice
        {
            get => _totalSellingPrice;
            set
            {
                if (SetProperty(ref _totalSellingPrice, value))
                    OnPropertyChanged(nameof(SellingPricePerUnit));
            }
        }

        public decimal SellingPricePerUnit => QuantityUnits > 0 ? TotalSellingPrice / QuantityUnits : 0;

        public decimal UnitProfit => SellingPricePerUnit - UnitCost;
        public double ProfitMargin => UnitCost > 0 ? (double)(UnitProfit / UnitCost) * 100 : 0;

        // Keep this for database compatibility (unit price)
        public decimal SellingPrice
        {
            get => SellingPricePerUnit;
            set => TotalSellingPrice = value * QuantityUnits;
        }

    }
}
