# D.Chemist - Technical Analysis and Limitations

## Project Snapshot
- Stack: WinUI 3 (`net8.0-windows`), MVVM-style ViewModels, PostgreSQL (`Npgsql` + `Dapper`), desktop-first architecture.
- Main layers observed:
  - UI/ViewModels: `ViewModels/`
  - Domain/Data access: `Repositories/`
  - Infra/Integrations: `Services/`, `Database/`

## Key Technical Limitations

### 1) Security and secrets handling (High)
- Plain DB credentials are committed in config: `appsettings.json` (`Database:Password`).
- Default admin password is hardcoded in code path: `Database/DatabaseService.cs` (`"@dmin8787"` on first run).
- Update flow (`Services/UpdateService.cs`) downloads and executes updater without explicit signature/hash verification.

Impact:
- High risk if repository/build artifacts are exposed.
- Weakens operational security and compliance posture.

### 2) Startup/process management risks (High)
- `Program.cs` kills other running processes with same name if mutex is not new.
- This can terminate user sessions unexpectedly and risks data loss.

Impact:
- Unsafe multi-instance handling and potential corruption during active writes.

### 3) Database initialization/migration strategy is fragile (High)
- Very large in-app SQL bootstrap/migration block in `Database/DatabaseService.cs`.
- Migration logic includes destructive schema changes (`DROP COLUMN`) inline.
- `InitializeAsync()` catches exceptions and only logs, which can hide startup data-layer failure from UI.

Impact:
- Hard to reason about schema state across versions.
- Difficult rollback/recovery when production data evolves.

### 4) Transaction consistency bugs in sales/returns workflow (High)
- `SaleRepository.VoidSaleAsync()` opens a transaction, but reads sale/items via `GetSaleWithItemsAsync()` using a separate connection outside transaction.
- Similar mixed connection/transaction patterns increase race-condition windows under concurrent usage.

Impact:
- Potential inconsistent reads/writes, especially during high throughput or multi-terminal use.

### 5) Role model is effectively single-role (Medium)
- Authorization service maps almost all capabilities to `IsAdmin` only (`Services/AuthorizationService.cs`).
- Limited granular permissions (billing-only user, report-only user, etc.) despite role field existing in DB.

Impact:
- Operational inflexibility and over-privileged user accounts.

### 6) ViewModels contain heavy business orchestration (Medium)
- `ViewModels/BillingViewModel.cs` and `ViewModels/FinancialViewModel.cs` contain dense business flows (sale posting, FBR reporting, printing, return handling).
- Multiple responsibilities per method reduce testability and increase regression risk.

Impact:
- Harder maintenance and onboarding; higher chance of side-effects when modifying logic.

### 7) Async command error handling can swallow failures (Medium)
- `Utils/AsyncRelayCommand.cs` uses `async void Execute` with internal catch/log only.
- Errors may not propagate to calling flow for structured user feedback or retries.

Impact:
- Hidden runtime failures and difficult diagnosis in edge cases.

### 8) Configuration drift/inconsistency (Medium)
- Tax is read from both settings table and config depending on flow (`SettingsService` vs direct config usage in `FinancialViewModel`).
- Some hardcoded defaults exist in ViewModels (`ReceiptViewModel`) and DB seed scripts.

Impact:
- Inconsistent behavior across screens and environments.

### 9) Backup/restore robustness and portability gaps (Medium)
- Backup service depends on local `pg_dump`/`psql` paths and OS install assumptions (`Services/BackupService.cs`).
- Restore path can fail if executable path or version differs; no preflight capability check screen.

Impact:
- Operational failures on new machines; difficult support.

### 10) Missing automated tests and CI quality gates (High)
- No test projects found in solution.
- Core flows (sale creation, stock deduction FIFO, void, return, migration) are unprotected by automated regression tests.

Impact:
- High regression probability as codebase grows.

## Functions/Methods Requiring Priority Refactor

1. `DatabaseService.InitializeAsync()`  
   - Split into versioned migrations and explicit startup failure signaling.

2. `SaleRepository.CreateTransactionAsync()`  
   - Improve stock locking strategy and validate schema alignment (`fbr_reported` column usage vs schema definition).

3. `SaleRepository.VoidSaleAsync()`  
   - Ensure full read/write flow runs on one shared connection+transaction.

4. `BillingViewModel.ExecuteCompleteSaleAsync()`  
   - Move business orchestration to an application service (`SaleWorkflowService`).

5. `UpdateService.LaunchUpdater()` and `CheckForUpdatesAsync()`  
   - Add signed package verification (or SHA-256 pinning) before execution.

6. `Program.Main()`  
   - Replace process-kill behavior with graceful single-instance activation/bring-to-front pattern.

## Prioritized TODO List

## Phase 3 - Architecture and maintainability
- [ ] Extract sale orchestration from `BillingViewModel` into dedicated service(s).
- [ ] Extract financial actions (void/reprint/return) from `FinancialViewModel` into service layer.
- [ ] Introduce DTOs/use-cases to reduce direct ViewModel-to-repository coupling.
- [ ] Normalize config reads (single source for tax/settings/runtime flags).

## Phase 4 - Quality and observability
- [ ] Add unit tests for repositories with transaction-aware scenarios.
- [ ] Add integration tests for: create sale, partial return, full void, stock restoration.
- [ ] Add migration tests for upgrade paths from older schema versions.
- [ ] Add startup health checks and user-facing fatal init message if DB bootstrap fails.

## Phase 5 - Operations and deployment
- [ ] Improve backup/restore preflight checks (tool existence, DB connectivity, permissions).
- [ ] Add structured logging context (user id, bill no, correlation id) across critical flows.
- [ ] Add CI pipeline with build + tests + static checks before release packaging.

## Suggested Immediate Next 3 Tasks
- [ ] Task A: Fix `VoidSaleAsync()` transaction boundary bug and add regression test.
- [ ] Task B: Remove secrets/default password from code/config and implement first-run credential setup.
- [ ] Task C: Implement update file integrity validation before updater launch.

