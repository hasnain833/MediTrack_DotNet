using System.Text.Json;
using Dapper;
using Npgsql;
using DChemist.Utils;
using DChemist.Repositories;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
    Console.WriteLine($"PASS: {message}");
}

Check(ExpiryPolicy.NeedsAttention(DateTime.Today.AddDays(-1), 1), "Expired stock is included");
Check(ExpiryPolicy.NeedsAttention(DateTime.Today, 1), "Today's expiry is included");
Check(ExpiryPolicy.NeedsAttention(DateTime.Today.AddMonths(5), 1), "Five-month warning is included");
Check(ExpiryPolicy.NeedsAttention(ExpiryPolicy.Cutoff, 1), "Six-month boundary is inclusive");
Check(!ExpiryPolicy.NeedsAttention(ExpiryPolicy.Cutoff.AddDays(1), 1), "Beyond six months is excluded");
Check(!ExpiryPolicy.NeedsAttention(DateTime.Today.AddDays(-1), 0), "Empty expired stock is excluded");
Check(ExpiryPolicy.Cutoff == DateTime.Today.AddMonths(6), "Window uses calendar months");

int tabletsPerBox = MedicinePricing.TabletsPerBox(10, 10);
Check(tabletsPerBox == 100, "One box has packets times tablets per packet");
var tabletCost = MedicinePricing.ToTabletPrice(3222.25m, true, tabletsPerBox);
Check(tabletCost == 32.2225m, "Box purchase cost is saved as cost per tablet");
Check(MedicinePricing.ToEntryPrice(tabletCost, true, tabletsPerBox) == 3222.25m,
    "Editing a saved tablet cost restores the original box cost");
Check(MedicinePricing.ToTabletPrice(32.2225m, false, tabletsPerBox) == 32.2225m,
    "Tablet input is not divided by packaging");
Check(MedicinePricing.ToEntryPrice(32.2225m, false, tabletsPerBox) == 32.2225m,
    "Switching to Tablet displays the same per-tablet cost");
Check(MedicinePricing.ToTabletPrice(3889.08m, true, tabletsPerBox) == 38.8908m,
    "Selling price retains the existing box-to-tablet conversion");
Check(MedicinePricing.TabletsPerBox(1, 1) == 1, "Default packaging is one packet with one tablet");
Check(MedicinePricing.ToTabletPrice(0, true, tabletsPerBox) == 0, "Zero purchase cost stays zero");
bool invalidPackagingRejected = false;
try { MedicinePricing.TabletsPerBox(0, 10); }
catch (ArgumentOutOfRangeException) { invalidPackagingRejected = true; }
Check(invalidPackagingRejected, "Zero packaging is rejected before saving prices");

var percentBill = BillingTotals.Calculate(1000, 0, 0, 10, true, 200);
Check(percentBill.Discount == 100 && percentBill.Total == 1100, "Percentage discount applies only to medicine subtotal, never extra");
Check(BillingTotals.Calculate(1000, 0, 100, 95, false, 200) == percentBill, "Flat and percentage are alternative ways to enter the same discount");
Check(BillingTotals.Calculate(2000, 0, 0, 10, true, 200).Discount == 200, "Percentage discount recalculates when cart subtotal changes");
Check(BillingTotals.Calculate(1000, 0, 5000, 0, false, 200).Total == 200, "Flat discount cannot consume the extra charge");
Check(BillingTotals.Calculate(0, 0, 0, 10, true, 200).Total == 200, "An extra-only bill retains its full amount");
Check(BillingTotals.Calculate(1000, 0.1m, 0, 10, true, 200).Tax == 100, "Existing cart tax does not apply to the extra charge");

var receipt = new DChemist.ViewModels.ReceiptViewModel
{
    TotalAmount = 1000, DiscountAmount = 100, ExtraAmount = 200, GrandTotal = percentBill.Total
};
receipt.Items.Add(new() { Quantity = 10, Price = 100 });
var printed = DChemist.Services.ReceiptBuilder.BuildReceiptString(receipt);
Check(printed.Split('\n').Any(line => line.StartsWith("Discount") && line.Contains("-100")), "Thermal receipt still prints the discount");
Check(printed.Split('\n').Any(line => line.StartsWith("Extra amount") && line.Contains("200")) && printed.Contains($"TOTAL Rs {1100m:N2}"), "Thermal receipt prints extra separately and includes it in total");
receipt.ExtraAmount = 0;
Check(!DChemist.Services.ReceiptBuilder.BuildReceiptString(receipt).Contains("Extra amount"), "Zero extra does not create an empty receipt charge");

if (!args.Contains("--local-fixtures")) return;

// No shop records are read or changed: temporary tables shadow the application tables.
using var config = JsonDocument.Parse(File.ReadAllText(Path.Combine("appsettings.json")));
var db = config.RootElement.GetProperty("Database");
string Value(string key) => db.GetProperty(key).ToString();
Check(new[] { "localhost", "127.0.0.1", "::1" }.Contains(Value("Host")), "Fixtures only connect to a local server");
var builder = new NpgsqlConnectionStringBuilder
{
    Host = Value("Host"), Port = int.Parse(Value("Port")), Database = Value("Database"),
    Username = Value("User"), Password = Value("Password"), Timeout = 5
};
await using var conn = new NpgsqlConnection(builder.ConnectionString);
await conn.OpenAsync();
await using var tx = await conn.BeginTransactionAsync();
try
{
    await conn.ExecuteAsync(@"
        SET LOCAL search_path TO pg_temp;
        CREATE TEMP TABLE sales (id int, grand_total numeric, tax_amount numeric, discount_amount numeric,
            fbr_reported boolean, status text, sale_date timestamptz);
        CREATE TEMP TABLE sale_items (sale_id int, batch_id int, quantity int, returned_qty int, unit_price numeric, unit_cost_at_sale numeric);
        CREATE TEMP TABLE inventory_batches (id int, unit_cost numeric);
        INSERT INTO inventory_batches VALUES (1, 5);
        INSERT INTO sales VALUES
          (1, 95, 5, 10, false, 'Completed', @moment),
          (2, 60, 0, 0, false, 'Returned', @moment),
          (3, 100, 0, 0, false, 'Voided', @moment),
          (4, 0, 0, 0, false, 'Voided', @moment),
          (5, 999, 0, 0, false, 'Completed', @nextDay);
        INSERT INTO sale_items VALUES (1,1,10,0,10,5), (2,1,10,4,10,5), (3,1,10,0,10,5), (4,1,2,2,10,5);
        ", new { moment = DateTime.Today.AddHours(12).ToUniversalTime(), nextDay = DateTime.Today.AddDays(1).ToUniversalTime() }, tx);

    var day = FinancialReportQueries.ForDay(DateTime.Today);
    var report = await conn.QuerySingleAsync(FinancialReportQueries.Summary, day, tx);
    Check((decimal)report.netsales == 155m, "Refund is not subtracted twice; voids and next-day bills are excluded");
    Check((decimal)report.totalprofit == 70m, "Profit subtracts tax, bill discount, and retained stock cost");
    Check((decimal)report.totalreturns == 60m, "Partial and fully returned bills appear in return totals");
    Check((decimal)report.grosssales == 215m, "Gross less returns equals net sales");
    Check((int)report.totalsalescount == 2, "Bill count excludes voids");
    Check((int)report.missingcostitems == 0, "Known batch costs do not warn");

    await conn.ExecuteAsync("UPDATE inventory_batches SET unit_cost = 500", transaction: tx);
    report = await conn.QuerySingleAsync(FinancialReportQueries.Summary, day, tx);
    Check((decimal)report.totalprofit == 70m, "Restocking or editing a batch cannot change snapshotted sale profit");
    await conn.ExecuteAsync("INSERT INTO sale_items VALUES (2,999,1,0,10,NULL)", transaction: tx);
    report = await conn.QuerySingleAsync(FinancialReportQueries.Summary, day, tx);
    Check((int)report.missingcostitems == 1, "Missing historical batch costs are flagged");
    Check((int)report.estimatedcostitems == 1, "Legacy costs without a snapshot are identified as estimates");
    report = await conn.QuerySingleAsync(FinancialReportQueries.Summary, FinancialReportQueries.ForDay(DateTime.Today.AddDays(-2)), tx);
    Check((decimal)report.netsales == 0 && (decimal)report.totalprofit == 0, "Empty day reports zero");

    await conn.ExecuteAsync(File.ReadAllText("Database/Migrations/20261010_002_sale_extra_amount.sql"), transaction: tx);
    Check(await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sales WHERE extra_amount <> 0", transaction: tx) == 0,
        "Extra migration preserves older bills with zero extra");
    await conn.ExecuteAsync(@"
        CREATE TEMP SEQUENCE fixture_sale_ids START 1000;
        ALTER TABLE sales ALTER COLUMN id SET DEFAULT nextval('fixture_sale_ids');
        ALTER TABLE sales ALTER COLUMN status SET DEFAULT 'Completed';
        ALTER TABLE sales ADD bill_no text, ADD customer_id int, ADD user_id int,
            ADD total_amount numeric, ADD fbr_invoice_no text, ADD fbr_response text;
        ", transaction: tx);
    var extraSaleId = await conn.ExecuteScalarAsync<int>(QueryFrom("Repositories/SaleRepository.cs", "CreateTransactionAsync", "saleQuery"),
        new { billNo = "EXTRA-TEST", customerId = (int?)null, userId = 1, total = 0m, tax = 0m,
            discount = 0m, grandTotal = 200m, fbrReported = false, fbrInvoiceNo = (string?)null,
            fbrResponse = (string?)null, extraAmount = 200m }, tx);
    Check(await conn.ExecuteScalarAsync<decimal>("SELECT extra_amount FROM sales WHERE id=@id", new { id = extraSaleId }, tx) == 200,
        "Production sale insert saves extra for later receipt reprints");
    await conn.ExecuteAsync("CREATE TEMP TABLE users (id int, username text)", transaction: tx);
    var savedReceipt = await conn.QuerySingleAsync(QueryFrom("Repositories/SaleRepository.cs", "GetSaleWithItemsAsync", "saleQuery"),
        new { billNo = "EXTRA-TEST" }, tx);
    Check((decimal)savedReceipt.extraamount == 200 && (decimal)savedReceipt.grandtotal == 200,
        "Receipt reprint query retrieves the saved extra charge and total");
    report = await conn.QuerySingleAsync(FinancialReportQueries.Summary, day, tx);
    Check((decimal)report.netsales == 355 && (decimal)report.totalprofit == 270,
        "Dashboard and daily totals include an extra-only sale exactly once");

    await conn.ExecuteAsync(@"
        CREATE TEMP TABLE medicines (id int, name text, generic_name text, barcode text, category_id int,
            manufacturer_id int, gst_percent numeric, units_per_pack int, packets_per_box int, default_entry_mode text);
        CREATE TEMP TABLE categories (id int, name text);
        CREATE TEMP TABLE manufacturers (id int, name text);
        CREATE TEMP TABLE suppliers (id int, name text, phone text);
        CREATE TEMP TABLE purchase_invoices (id int, invoice_no text, invoice_date date);
        INSERT INTO suppliers VALUES (1, 'Fixture supplier', '');
        ALTER TABLE inventory_batches ADD medicine_id int, ADD supplier_id int, ADD batch_no text,
            ADD remaining_units int DEFAULT 0, ADD selling_price numeric DEFAULT 10,
            ADD expiry_date date, ADD entry_mode text DEFAULT 'Tablet', ADD units_per_pack int DEFAULT 1,
            ADD pack_quantity int DEFAULT 1, ADD invoice_no text, ADD invoice_date date,
            ADD purchase_invoice_id int, ADD created_at timestamptz DEFAULT now();
        INSERT INTO medicines SELECT n, 'Fixture ' || n, '', '', NULL, NULL, 0, 1, 1, 'Tablet'
            FROM generate_series(1001, 1250) n;
        INSERT INTO inventory_batches (id, medicine_id, supplier_id, batch_no, unit_cost, remaining_units, expiry_date)
            SELECT n, n, 1, 'Batch ' || n, 5, 1, @cutoff FROM generate_series(1001, 1250) n;
        INSERT INTO inventory_batches (id, medicine_id, batch_no, unit_cost, remaining_units, expiry_date) VALUES
            (2001,1001,'Expired without supplier',5,4,@yesterday),
            (2002,1001,'Beyond six months',5,2,@beyond),
            (2003,1001,'Empty expired',5,0,@yesterday);
        ", new { cutoff = ExpiryPolicy.Cutoff, yesterday = DateTime.Today.AddDays(-1), beyond = ExpiryPolicy.Cutoff.AddDays(1) }, tx);

    // Execute the production SQL, rather than a second implementation of the filters.
    string QueryFrom(string file, string method, string constant)
    {
        var source = File.ReadAllText(file);
        var methodStart = source.IndexOf(method, StringComparison.Ordinal);
        var marker = "const string " + constant + " = @\"";
        var start = source.IndexOf(marker, methodStart, StringComparison.Ordinal) + marker.Length;
        var end = source.IndexOf("\";", start, StringComparison.Ordinal);
        return source[start..end].Replace("\"\"", "\"");
    }
    var inventoryQuery = QueryFrom("Repositories/MedicineRepository.cs", "GetAllAsync(", "query");
    var inventoryParameters = new { pageSize = (int?)null, offset = 0, expiringOnly = true,
        expiryCutoff = ExpiryPolicy.Cutoff, text = (string?)null, exact = (string?)null };
    var inventory = (await conn.QueryAsync(inventoryQuery, inventoryParameters, tx)).ToList();
    Check(inventory.Count == 251, "Expiring inventory includes every stocked batch beyond the previous row limits");
    inventory = (await conn.QueryAsync(inventoryQuery, new { pageSize = (int?)null, offset = 0, expiringOnly = true,
        expiryCutoff = ExpiryPolicy.Cutoff, text = "%Fixture 1001%", exact = "Fixture 1001" }, tx)).ToList();
    Check(inventory.Count == 2, "Search retains both expired and near-expiry batches of the same medicine");

    var exportQuery = QueryFrom("Repositories/BatchRepository.cs", "GetExpiringStockAsync(", "query");
    var export = (await conn.QueryAsync(exportQuery, new { expiryCutoff = ExpiryPolicy.Cutoff }, tx)).ToList();
    Check(export.Count == 251 && export.Count(r => r.supplierid == null) == 1,
        "Supplier export finds the same stock and identifies missing suppliers");
    var attentionQuery = QueryFrom("Repositories/DashboardRepository.cs", "GetAttentionItemsAsync(", "sql");
    var attention = (await conn.QueryAsync(attentionQuery, new { expiryCutoff = ExpiryPolicy.Cutoff }, tx)).ToList();
    Check(attention.Count(r => r.kind == "exp") == 251, "Dashboard expiry count is not capped at 200");
    var deductionQuery = QueryFrom("Repositories/SaleRepository.cs", "CreateTransactionAsync(", "getBatchesQuery");
    var sellable = (await conn.QueryAsync(deductionQuery, new { medId = 1001, today = DateTime.Today }, tx)).ToList();
    Check(sellable.Count == 2 && sellable.Sum(r => (int)r.remaining_units) == 3,
        "Checkout excludes expired stock and still allows valid stock beyond the warning window");
}
finally
{
    await tx.RollbackAsync();
}
