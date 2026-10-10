# Shop PC update and report checks

This update uses six calendar months for expiry warnings, counts, filtered searches, and supplier return sheets. Expired stock remains included; empty stock is excluded. Stock with no supplier appears in Items but needs a supplier before it can be exported.

The dashboard and daily report now share their net-sales and profit calculation. Refunds already reduce stored bill totals, so they are not deducted twice. Profit is retained bill revenue excluding tax, less retained stock cost; bill discounts are reflected in revenue. Reports use the PC's local calendar day. Returns are attributed to the original bill date, because the current database does not record separate refund timestamps.

New sales store the cost of the stock actually deducted. Restocking or editing a batch will no longer change their reported profit. Old sales have no reliable sale-time cost snapshot; their profit uses the current linked batch cost and the daily report warns that it is an estimate. Nothing automatically rewrites old sales or purchase costs.

The medicine form labels packaging as "Packets in one box" and "Tablets in one packet", with new packaging fields initially empty. New medicine creation does not ask for a stock quantity; stock is added through Purchase. Editing in Box mode shows the purchase cost per box and converts it to per-tablet cost on save. Tablet mode accepts per-tablet cost directly. Changing mode converts both displayed purchase and selling prices. Existing incorrect costs must still be verified and corrected manually; this update does not assume they were box costs.

## Install on the shop PC

Billing shows one Discount row with percentage and PKR inputs side by side. Entering a value in one clears the other; the inputs never auto-convert each other. Enter on an empty medicine search starts checkout at Discount %, then PKR, Cash, Name, and Phone. Enter on Phone saves and prints. Ctrl+S saves without printing; Ctrl+P saves and prints from any billing input. The buttons are labeled Print bill and Save. There is no duplicate discount summary row under the inputs. Extra has been removed from new billing. Previously saved extra charges remain in totals and receipt reprints; their outside purchase cost was not tracked by that field. Both save commands are disabled while a bill is being processed to prevent duplicate saves.

1. Sign in as Admin and use Settings to make a database backup. Keep the SQL backup and a copy of the current application folder somewhere outside that folder.
2. Close D. Chemist on every PC using this database before the update. In Task Manager, confirm it has exited. Do not reset or restore the database just to fix report totals.
3. Copy the complete new published application folder to the shop, including DLLs, assets, updater, and `Database/Migrations`. Keep the shop's existing `appsettings.json`; it contains the correct database connection and local settings. Do not replace it with the development PC's configuration. Keep the shop's backup and log folders.
4. Check Windows Settings → Time & language → Date & time. For this shop, use Pakistan Standard Time (UTC+05:00), with the correct date and time. Daily boundaries now follow the PC timezone.
5. Start the updated app. It waits for database setup before allowing login. The sale-cost snapshot and extra amount columns are created by migrations automatically. Confirm the login screen version is **1.9.6.0**. If database setup fails, check the app logs and connection; do not continue using a partially updated database.
6. Open Daily Report for the affected date, and compare its Net Sales and Profit with the dashboard for today's date. Bills marked Voided do not count. Review any warning about older or missing costs.
7. Check an expired batch, a batch expiring in five months, and one beyond six months in Items → Expiring. Export a supplier return sheet. A supplier-less batch should produce an explanation rather than disappear silently.

The automatic updater offers this release after the GitHub package and its version manifest are published. It preserves the shop's database connection settings. The release ZIP excludes development settings and debug symbols.

## Investigate the negative profit in the screenshot

The screenshot shows 17 bills, PKR 7,783.08 sales and PKR -82,156.92 profit on 10 October 2026. The old formula implies cost approximately PKR 89,940 if the retained item sales equal the displayed bill sales. Discounts, tax and inconsistent older bill rows can affect that comparison. It does not prove which costs are wrong.

Open pgAdmin on the shop PC, select the pharmacy database, open Query Tool, and run `Database/Diagnostics/shop_report_check.sql`. The file contains SELECT queries only and works before or after this update. It checks:

- Stored bill totals, total cost, and the old versus corrected report calculation.
- Which medicines contribute the largest costs, with their recorded per-unit cost, selling price, purchase total, quantity, and pack size.
- Bills whose saved totals disagree with their quantities and unit prices.

For the largest cost rows, compare the original supplier invoice. Example: a box costing PKR 1,000 with 100 tablets has a tablet cost of PKR 10, adjusted for any actual discount and bonus units. If the stored tablet cost is PKR 1,000, the unit scale is wrong. Do not assume every loss-making item has this error, or divide every cost by the same pack size.

If an invoice confirms the **current batch** cost is wrong, correct Cost / tablet in Items for that batch, then reopen the report. Earlier bills without snapshots will also use that corrected current cost. That is accurate only if those bills used the same purchase cost. If a batch has since been repurchased or edited, use original invoices and batch history to recover the historical cost; changing today's cost is not a reliable repair for all older sales.

Send the affected date, one bill's item details (quantity, sale price and cost), the matching supplier invoice figures, and the diagnostic results before applying a historical repair. Exact corrections require evidence per batch or sale. The update fixes the known calculations but cannot invent correct missing history.

## Developer verification and packaging

Run `dotnet run --project Tests/RegressionChecks.csproj` for expiry boundaries, packaging conversions, discount/extra totals, and thermal receipt output. Add `-- --local-fixtures` to test report SQL, saved extra charges and reprint queries with disposable temporary tables on the locally configured PostgreSQL server. The fixture transaction rolls back and does not read or update shop records.

Publish with:

```powershell
dotnet publish DChemist.csproj --no-restore -c Release -r win-x64 --self-contained -p:Platform=x64 -o Publish/Billing-1.9.6.0
```

Transfer the complete output, preserving shop configuration as described above. To distribute through the automatic updater later, commit the reviewed changes and use the existing release script with version 1.9.6.0 and release notes. Publishing to GitHub is a separate step.
