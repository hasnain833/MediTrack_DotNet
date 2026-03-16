## 9. New Audit Findings (March 17, 2026)

### 🔴 Critical: Broken Automated Backups
- **Description**: `BackupService` attempts to read the connection string from `DefaultConnection`, which does not exist in `appsettings.json`.
- **Danger**: Automated backups are failing silently or with errors, leaving data unprotected.
- **Location**: `BackupService.cs` (Lines 60, 151)
- **Solution**: Update `BackupService` to read from the `Database` section, consistent with `DatabaseService.cs`.

### 🟡 High: Hardcoded Metadata in Stock Entry
- **Description**: `StockInViewModel` hardcodes drug categories and manufacturers.
- **Problem**: Adding new categories or manufacturers in the database won't reflect in the stock entry form.
- **Location**: `StockInViewModel.cs` (Lines 57-79)
- **Solution**: Inject `CategoryRepository` and `ManufacturerRepository` to load these lists dynamically.

### 🟡 High: Redundant Database Lookups
- **Description**: `StockInViewModel` performs a full database lookup immediately after creating a new medicine record.
- **Problem**: Inefficient performance and potential race conditions.
- **Location**: `StockInViewModel.cs` (Lines 400-412)
- **Solution**: Update `MedicineRepository.AddAsync` to return the created object or ID, and use it directly.

### 🔵 Medium: UI Responsiveness Optimization
- **Description**: `InventoryPage.xaml` lacks a root scroll viewer, risk of clipping on small screens.
- **Location**: `InventoryPage.xaml`
- **Solution**: Wrap main grid or specific sections in a `ScrollViewer`, similar to `InventoryAdjustmentPage`.

---

**Audit Updated By**: Senior .NET Architect
**Status**: Critical Fixes Addressed (Previously). New findings documented for immediate execution.
