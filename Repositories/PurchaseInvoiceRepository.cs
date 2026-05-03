using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Threading.Tasks;
using DChemist.Database;
using DChemist.Models;
using DChemist.Utils;
using DChemist.Services;
using Dapper;

namespace DChemist.Repositories
{
    public class PurchaseInvoiceRepository
    {
        private readonly DatabaseService _db;

        public PurchaseInvoiceRepository(DatabaseService db)
        {
            _db = db;
        }

        public async Task<int> AddAsync(PurchaseInvoice invoice, IDbConnection? existingConn = null, IDbTransaction? transaction = null)
        {
            const string query = @"
                INSERT INTO purchase_invoices (invoice_no, supplier_id, invoice_date, total_amount, status)
                VALUES (@InvoiceNo, @SupplierId, @InvoiceDate, @TotalAmount, @Status)
                RETURNING id";

            if (existingConn != null)
            {
                return await existingConn.ExecuteScalarAsync<int>(query, invoice, transaction);
            }

            using var conn = _db.GetConnection();
            return await conn.ExecuteScalarAsync<int>(query, invoice);
        }

        public async Task<List<PurchaseInvoice>> GetAllAsync()
        {
            try
            {
                const string query = @"
                    SELECT i.*, s.name as SupplierName
                    FROM purchase_invoices i
                    LEFT JOIN suppliers s ON s.id = i.supplier_id
                    ORDER BY i.invoice_date DESC
                    LIMIT 500";
                using var conn = _db.GetConnection();
                var rows = await conn.QueryAsync<PurchaseInvoice>(query);
                return rows.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError("PurchaseInvoiceRepository.GetAllAsync failed", ex);
                return new List<PurchaseInvoice>();
            }
        }

        public async Task<List<InventoryBatch>> GetInvoiceItemsAsync(int invoiceId)
        {
            try
            {
                const string query = @"
                    SELECT b.*, m.name as MedicineName
                    FROM inventory_batches b
                    JOIN medicines m ON m.id = b.medicine_id
                    WHERE b.purchase_invoice_id = @invoiceId";
                using var conn = _db.GetConnection();
                var rows = await conn.QueryAsync<InventoryBatch>(query, new { invoiceId });
                return rows.ToList();
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"PurchaseInvoiceRepository.GetInvoiceItemsAsync failed for id={invoiceId}", ex);
                return new List<InventoryBatch>();
            }
        }
        public async Task ProcessStockInAsync(string supplierName, string invoiceNo, DateTime date, List<ReceivingItem> items)
        {
            using var conn = _db.GetConnection();
            await conn.OpenAsync();
            using var transaction = await conn.BeginTransactionAsync();

            try
            {
                // 1. Get or Create Supplier
                var supplierId = await conn.ExecuteScalarAsync<int?>(
                    "SELECT id FROM suppliers WHERE LOWER(name) = LOWER(@name) LIMIT 1", 
                    new { name = supplierName }, transaction);
                
                if (!supplierId.HasValue)
                {
                    supplierId = await conn.ExecuteScalarAsync<int>(
                        "INSERT INTO suppliers (name) VALUES (@name) RETURNING id", 
                        new { name = supplierName }, transaction);
                }

                // 2. Create Purchase Invoice
                var invoiceId = await conn.ExecuteScalarAsync<int>(@"
                    INSERT INTO purchase_invoices (invoice_no, supplier_id, invoice_date, total_amount, status)
                    VALUES (@invoiceNo, @supplierId, @date, @total, 'Completed')
                    RETURNING id", 
                    new { invoiceNo, supplierId, date, total = items.Sum(i => i.PurchaseTotalPrice) }, 
                    transaction);

                // 3. Process Items: Upsert into "Standard" Batch (where stock is actually stored)
                foreach (var item in items)
                {
                    // We check if a 'Standard' batch exists for this medicine.
                    // This is where your stock info (Qty, Price) actually lives.
                    var existingBatchId = await conn.ExecuteScalarAsync<int?>(
                        "SELECT id FROM inventory_batches WHERE medicine_id = @MedicineId AND batch_no = 'Standard' LIMIT 1",
                        new { item.MedicineId }, transaction);

                    if (existingBatchId.HasValue)
                    {
                        // Update existing Standard batch
                        await conn.ExecuteAsync(@"
                            UPDATE inventory_batches 
                            SET quantity_units = quantity_units + @QuantityUnits,
                                remaining_units = remaining_units + @QuantityUnits,
                                purchase_total_price = @PurchaseTotalPrice,
                                unit_cost = @UnitCost,
                                selling_price = @SellingPrice,
                                purchase_invoice_id = @invoiceId,
                                expiry_date = @ExpiryDate
                            WHERE id = @batchId",
                            new { 
                                item.QuantityUnits, 
                                item.PurchaseTotalPrice, 
                                item.UnitCost, 
                                SellingPrice = item.SellingPricePerUnit, 
                                invoiceId, 
                                item.ExpiryDate,
                                batchId = existingBatchId.Value 
                            }, transaction);
                    }
                    else
                    {
                        // Create a new Standard batch if it doesn't exist
                        await conn.ExecuteAsync(@"
                            INSERT INTO inventory_batches (
                                medicine_id, supplier_id, batch_no, quantity_units, 
                                purchase_total_price, unit_cost, selling_price, 
                                remaining_units, expiry_date, invoice_no, invoice_date,
                                entry_mode, units_per_pack, pack_quantity, purchase_invoice_id
                            )
                            VALUES (
                                @MedicineId, @supplierId, 'Standard', @QuantityUnits, 
                                @PurchaseTotalPrice, @UnitCost, @SellingPricePerUnit, 
                                @QuantityUnits, @ExpiryDate, @invoiceNo, @date,
                                @EntryMode, @UnitsPerPack, @PackQuantity, @invoiceId
                            )",
                            new {
                                item.MedicineId,
                                supplierId,
                                item.QuantityUnits,
                                item.PurchaseTotalPrice,
                                item.UnitCost,
                                SellingPricePerUnit = item.SellingPricePerUnit,
                                item.ExpiryDate,
                                invoiceNo,
                                date,
                                item.EntryMode,
                                item.UnitsPerPack,
                                item.PackQuantity,
                                invoiceId
                            }, transaction);
                    }
                }

                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                AppLogger.LogError("ProcessStockInAsync failed — transaction rolled back", ex);
                throw;
            }
        }
    }
}
