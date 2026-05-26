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
                    SELECT b.*, m.name as MedicineName, m.packets_per_box as PacketsPerBox
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

                // 3. Process each item — update existing batch OR create new one
                foreach (var item in items)
                {
                    // ── Load the medicine's packaging dimensions from DB ──────────────
                    var med = await conn.QuerySingleOrDefaultAsync<dynamic>(
                        "SELECT packets_per_box, units_per_pack FROM medicines WHERE id = @id",
                        new { id = item.MedicineId }, transaction);

                    int packetsPerBox = (int)(med?.packets_per_box ?? 1);
                    int unitsPerPack  = (int)(med?.units_per_pack  ?? 1);

                    // Total tablets = boxes × packets/box × tablets/packet
                    // EntryMode tells us if PackQuantity is in boxes or individual tablets
                    int totalUnits = item.EntryMode == "Box"
                        ? item.PackQuantity * packetsPerBox * unitsPerPack
                        : item.PackQuantity;  // Tablet mode: PackQuantity IS the tablet count

                    // Cost per tablet = total purchase price ÷ total tablets
                    decimal unitCost = totalUnits > 0 ? item.PurchaseTotalPrice / totalUnits : 0;

                    // ── Find the most recent existing batch for this medicine with the same batch number ─────────
                    // This matches an existing batch of the same batch number (or a placeholder),
                    // preventing duplicate rows from being inserted.
                    var existingBatch = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                        SELECT id
                        FROM inventory_batches
                        WHERE medicine_id = @MedicineId AND LOWER(batch_no) = LOWER(@BatchNo)
                        ORDER BY created_at DESC
                        LIMIT 1",
                        new { item.MedicineId, item.BatchNo }, transaction);

                    if (existingBatch != null)
                    {
                        // ── UPDATE: add stock to existing batch ──────────────────────
                        long batchId = Convert.ToInt64(existingBatch.id);
                        await conn.ExecuteAsync(@"
                            UPDATE inventory_batches 
                            SET quantity_units       = quantity_units + @totalUnits,
                                remaining_units      = remaining_units + @totalUnits,
                                purchase_total_price = @PurchaseTotal,
                                unit_cost            = @unitCost,
                                selling_price        = @SellingPrice,
                                pack_quantity        = pack_quantity + @packQty,
                                entry_mode           = @EntryMode,
                                units_per_pack       = @unitsPerPack,
                                purchase_invoice_id  = @invoiceId,
                                expiry_date          = @ExpiryDate
                            WHERE id = @batchId",
                            new {
                                totalUnits,
                                PurchaseTotal = item.PurchaseTotalPrice,
                                unitCost,
                                SellingPrice  = item.SellingPricePerUnit,
                                packQty       = item.PackQuantity,
                                item.EntryMode,
                                unitsPerPack,
                                invoiceId,
                                item.ExpiryDate,
                                batchId
                            }, transaction);

                        AppLogger.LogInfo($"[StockIn] Updated batch {batchId} (Batch: {item.BatchNo}) for medicine {item.MedicineId}: +{totalUnits} units @ {unitCost:N4}/unit.");
                    }
                    else
                    {
                        // ── INSERT: first-ever or new batch for this medicine ───────────────
                        await conn.ExecuteAsync(@"
                            INSERT INTO inventory_batches (
                                medicine_id, supplier_id, batch_no, quantity_units, 
                                purchase_total_price, unit_cost, selling_price, 
                                remaining_units, expiry_date, invoice_no, invoice_date,
                                entry_mode, units_per_pack, pack_quantity, purchase_invoice_id
                            )
                            VALUES (
                                @MedicineId, @supplierId, @BatchNo, @totalUnits, 
                                @PurchaseTotal, @unitCost, @SellingPricePerUnit, 
                                @totalUnits, @ExpiryDate, @invoiceNo, @date,
                                @EntryMode, @unitsPerPack, @PackQuantity, @invoiceId
                            )",
                            new {
                                item.MedicineId,
                                supplierId,
                                item.BatchNo,
                                totalUnits,
                                PurchaseTotal        = item.PurchaseTotalPrice,
                                unitCost,
                                item.SellingPricePerUnit,
                                item.ExpiryDate,
                                invoiceNo,
                                date,
                                item.EntryMode,
                                unitsPerPack,
                                item.PackQuantity,
                                invoiceId
                            }, transaction);

                        AppLogger.LogInfo($"[StockIn] Created new batch {item.BatchNo} for medicine {item.MedicineId}: {totalUnits} units @ {unitCost:N4}/unit.");
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
