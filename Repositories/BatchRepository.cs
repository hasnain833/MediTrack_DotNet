using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MediTrack.Database;
using MediTrack.Models;
using MediTrack.Services;
using Npgsql;

namespace MediTrack.Repositories
{
    public class BatchRepository
    {
        private readonly DatabaseService _db;
        private readonly AuthorizationService _auth;

        public BatchRepository(DatabaseService db, AuthorizationService auth)
        {
            _db = db;
            _auth = auth;
        }

        public async Task<List<InventoryBatch>> GetByMedicineIdAsync(int medicineId)
        {
            const string query = "SELECT * FROM inventory_batches WHERE medicine_id = @medId ORDER BY expiry_date ASC";
            var parameters = new Dictionary<string, object> { { "@medId", medicineId } };
            return await _db.FetchAllAsync(query, MapBatch, parameters);
        }

        private static InventoryBatch MapBatch(NpgsqlDataReader reader)
        {
            return new InventoryBatch
            {
                Id              = Convert.ToInt32(reader["id"]),
                MedicineId      = Convert.ToInt32(reader["medicine_id"]),
                SupplierId      = Convert.ToInt32(reader["supplier_id"]),
                BatchNumber     = reader["batch_number"].ToString() ?? string.Empty,
                PurchasePrice   = Convert.ToDecimal(reader["purchase_price"]),
                SellingPrice    = Convert.ToDecimal(reader["selling_price"]),
                StockQty        = Convert.ToInt32(reader["stock_qty"]),
                ManufactureDate = reader["manufacture_date"] != DBNull.Value ? Convert.ToDateTime(reader["manufacture_date"]) : null,
                ExpiryDate      = Convert.ToDateTime(reader["expiry_date"]),
                CreatedAt       = Convert.ToDateTime(reader["created_at"])
            };
        }

        public async Task AddAsync(InventoryBatch batch)
        {
            _auth.EnforceAdmin();
            const string query = @"
                INSERT INTO inventory_batches (medicine_id, supplier_id, batch_number, purchase_price, selling_price, stock_qty, manufacture_date, expiry_date)
                VALUES (@medId, @supId, @batch, @pPrice, @sPrice, @qty, @mDate, @eDate)";
            
            var parameters = new Dictionary<string, object>
            {
                { "@medId", batch.MedicineId },
                { "@supId", batch.SupplierId },
                { "@batch", batch.BatchNumber },
                { "@pPrice", batch.PurchasePrice },
                { "@sPrice", batch.SellingPrice },
                { "@qty", batch.StockQty },
                { "@mDate", batch.ManufactureDate?.ToString("yyyy-MM-dd") ?? (object)DBNull.Value },
                { "@eDate", batch.ExpiryDate.ToString("yyyy-MM-dd") }
            };

            await _db.ExecuteNonQueryAsync(query, parameters);
        }
    }
}
