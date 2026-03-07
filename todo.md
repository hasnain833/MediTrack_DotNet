# MediTrack - Project Status & TODO List

This document outlines the current state of the MediTrack project and upcoming tasks.

## ✅ Implemented Features
- [x] **Premium UI/UX**: Blue/Teal Glassmorphism theme implemented across all pages.
- [x] **Authentication**: Secure login system with `AuthService`.
- [x] **Dashboard**: Real-time metrics and overall system overview.
- [x] **Inventory Management**: Complete CRUD (Create, Read, Update, Delete) for medicines, including category and supplier tracking.
- [x] **Basic Billing**:
    - Search medicine by name.
    - Cart management (add, remove, update quantity).
    - Subtotal, Tax (18%), and Discount calculation.
    - Transaction recording in local database.
- [x] **Navigation**: Sidebar-based navigation system.
- [x] **QR/Barcode Scanning**:
    - [x] Add `Barcode` field to `Medicine` model.
    - [x] Implement auto-add to cart on successful scan in `BillingPage`.
- [x] **Database Normalization**:
    - [x] Fully normalized SQLite/PostgreSQL schema (Categories, Manufacturers, Suppliers, Batches).
    - [x] Batch-aware inventory tracking (First-to-expire logic).
- [x] **PostgreSQL Migration**:
    - [x] Swapped SQLite for PostgreSQL for multi-PC local network support.
    - [x] Centralized configuration via `appsettings.json`.

## 🛠️ Remaining / In-Progress
- [ ] **Thermal Receipt Printing & Fiscalization**:
    - Develop `PrintService` for compact thermal receipt printer (58mm/80mm).
    - Design high-quality receipt layout including FBR QR codes.
    - Automatically print receipt on "Complete Sale".
- [ ] **FBR (Federal Board of Revenue) Connection**:
    - Implement `FbrService` for real-time sales reporting (Fiscalization).
    - Store FBR Invoice numbers in the `Sale` record.
- [ ] **Advanced Analytics**:
    - Detailed sales reports by category, period, and user.
    - Low stock alerts and expiry notifications.
- [ ] **Reporting**:
    - Exporting sales and inventory reports to PDF/Excel.

## 🚀 Future Enhancements
- [ ] **Role-based Access Control**: Re-introduce if multi-user support is needed.
- [ ] **Cloud Sync**: Optional synchronization with a remote server.
- [ ] **Supplier Management**: Detailed records and purchase orders.
