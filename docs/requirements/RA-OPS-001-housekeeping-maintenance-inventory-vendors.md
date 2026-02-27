# RA-OPS-001 — Housekeeping, Maintenance, Inventory & Vendor Ops Requirements

| Field | Value |
|-------|-------|
| **Doc ID** | RA-OPS-001 |
| **Title** | Housekeeping, Maintenance, Inventory & Vendor Ops |
| **Status** | DRAFT |
| **Author** | Atlas Architecture |
| **Created** | 2026-02-27 |
| **Depends on** | RA-AUTO-001 (Automation Engine), RA-DASH-001 (Dashboards) |
| **Consumers** | Staff, Tenant Owners, Atlas Admin |

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Housekeeping Module (V1)](#2-housekeeping-module-v1)
3. [Maintenance / Work Orders (V1)](#3-maintenance--work-orders-v1)
4. [Inventory & Consumables (V1)](#4-inventory--consumables-v1)
5. [Vendors & Purchase Tracking (V1)](#5-vendors--purchase-tracking-v1)
6. [Incidents & Damage Claims (V1)](#6-incidents--damage-claims-v1)
7. [Staff Management (Lean V1)](#7-staff-management-lean-v1)
8. [Reporting Requirements (Ops)](#8-reporting-requirements-ops)
9. [Data Model & APIs (LLR)](#9-data-model--apis-llr)
10. [Observability & Audit](#10-observability--audit)
11. [Acceptance Criteria & Test Matrix](#11-acceptance-criteria--test-matrix)
12. [Rollout Plan](#12-rollout-plan)
13. [Definition of Done — Ops Core V1](#13-definition-of-done--ops-core-v1)

**Appendix A** — [Event Types (Ops Domain)](#appendix-a--event-types-ops-domain)
**Appendix B** — [Cross-Reference to Existing Models](#appendix-b--cross-reference-to-existing-models)

---

## 1. Executive Summary

Atlas PMS must be **indispensable for daily property operations** — not only a booking and channel manager but the single system staff open every morning. This addendum defines lean, reliable operational modules covering housekeeping, maintenance, inventory, vendor management, incidents, and staff coordination.

### 1.1 Design Principles

| Principle | Implication |
|-----------|-------------|
| **Daily-value first** | Solve the "morning checklist" problem — what needs cleaning, who does it, what's broken, what's running low |
| **Mobile-first staff UI** | Housekeeping/maintenance staff work on phones; every screen must be touch-friendly and work on slow 4G |
| **Automation-ready** | All task creation plugs into the existing DB-backed outbox + `AutomationSchedule` infrastructure (RA-AUTO-001) |
| **Tenant-scoped** | Every entity implements `ITenantOwnedEntity`; global query filter prevents cross-tenant leakage |
| **Lean V1** | No complex ERP; no procurement workflows; no accounting integration. Ship the 80% that saves 80% of time |
| **India context** | INR currency, GSTIN optional, WhatsApp as primary staff channel, Hindi/English support later |

### 1.2 V1 vs V2 Scope Boundary

| Capability | V1 (Rule-based, lean) | V2 (AI-assisted, extended) |
|------------|----------------------|---------------------------|
| Housekeeping | Auto-create from checkout, checklist templates, SLA, Kanban board | Photo-proof QC, AI damage detection, predictive scheduling |
| Maintenance | Ticket lifecycle, severity, SLA, vendor link, cost tracking | Predictive maintenance from IoT signals, auto-dispatch |
| Inventory | Stock in/out, low-stock alerts, per-checkin auto-deduct | Demand forecasting, auto-reorder, supplier marketplace |
| Vendors | Directory, expenses, payment status, monthly report | Vendor rating, contract management, auto-PO |
| Incidents | Log, link to booking/guest, cost estimate | Photo evidence AI, insurance claim draft, guest deposit auto-deduct |
| Staff | Profiles, roles, workload distribution | Attendance, shifts, performance scoring, geo-fencing |

---

## 2. Housekeeping Module (V1)

### 2.1 Cleaning Task Lifecycle

```
AUTO_CREATED ──► ASSIGNED ──► IN_PROGRESS ──► QC_PENDING ──► COMPLETED
       │              │              │               │              │
       │              │              │               ▼              │
       │              │              │           REOPENED ──► IN_PROGRESS
       │              ▼              ▼
       │         CANCELLED      CANCELLED
       ▼
  UNASSIGNED (manual creation, no auto-match)
```

| Status | Description | Transition Rules |
|--------|-------------|-----------------|
| `AUTO_CREATED` | System creates task when checkout event fires or schedule triggers | Auto → `ASSIGNED` if assignment rules match a staff member; otherwise stays `AUTO_CREATED` (acts as unassigned) |
| `ASSIGNED` | Staff member assigned | Staff taps "Start" → `IN_PROGRESS` |
| `IN_PROGRESS` | Cleaning underway | Staff taps "Done" → `QC_PENDING` if QC is enabled; otherwise → `COMPLETED` |
| `QC_PENDING` | Awaiting inspection by supervisor | Inspector taps "Approve" → `COMPLETED`; "Reject" → `REOPENED` |
| `COMPLETED` | Unit is clean and ready | Terminal state; sets room status to `READY` |
| `REOPENED` | Failed QC, needs re-cleaning | Same flow as `IN_PROGRESS` |
| `CANCELLED` | Booking cancelled or task created in error | Terminal state |

> **OPS-HK-001**: Every status transition MUST be recorded in `AuditLog` with `EntityType = 'HousekeepingTask'`.

### 2.2 Cleaning Types

| Type | Code | Trigger | Typical Duration |
|------|------|---------|-----------------|
| **Turnover** | `TURNOVER` | Guest checkout → next guest checkin | 60–120 min |
| **Mid-stay** | `MID_STAY` | Scheduled every N days during stay (configurable per property, default 3) | 30–45 min |
| **Deep clean** | `DEEP_CLEAN` | Manual or after long vacancy (>7 days), or quarterly schedule | 120–240 min |
| **Touch-up** | `TOUCH_UP` | Guest complaint or pre-checkin spot-fix | 15–30 min |

> **OPS-HK-002**: Turnover cleaning is the only type auto-created in V1. Mid-stay and deep-clean are manually created or via scheduled automation rules (RA-AUTO-001 `AutomationRule` with `TriggerType = TIME`).

### 2.3 SLA Rules

| Rule ID | Rule | Default | Configurable? |
|---------|------|---------|---------------|
| **OPS-HK-SLA-001** | Turnover cleaning MUST be completed before next check-in time | Check-in time from `Listing.CheckInTime` | Per property |
| **OPS-HK-SLA-002** | If no next booking, turnover MUST complete within 4 hours of checkout | 4 hours | Per property (1–8 hours) |
| **OPS-HK-SLA-003** | If task not `ASSIGNED` within 30 min of creation → alert property manager | 30 min | Per property (15–120 min) |
| **OPS-HK-SLA-004** | If task not `COMPLETED` by 2 hours before next check-in → escalate to tenant owner | 2 hours | Per property (1–4 hours) |
| **OPS-HK-SLA-005** | Mid-stay cleaning MUST complete within 2 hours of start | 2 hours | Per property |

> **OPS-HK-003**: SLA deadlines are stored as `DueAtUtc` on the `HousekeepingTask`. The existing `EscalationSweepJob` (RA-AUTO-001) polls overdue tasks and triggers escalation events via `OutboxMessage`.

### 2.4 Staff Assignment Rules

**V1: Role-based assignment**

| Priority | Rule |
|----------|------|
| 1 | If property has staff with role `housekeeping` → round-robin assign among active staff for that property |
| 2 | If no housekeeping staff → assign to `manager` role for property |
| 3 | If no staff at all → leave as `AUTO_CREATED` (unassigned) and alert tenant owner |

**V2 (future)**: Shift-based assignment, workload balancing, geo-proximity.

> **OPS-HK-004**: Assignment is attempted at task creation time. If no eligible staff, the task remains unassigned and an `OutboxMessage` with event type `ops.housekeeping.unassigned` is created.

### 2.5 Checklists

Each property can have a **checklist template** defining items to verify during cleaning. Templates are tenant-scoped with property-level override.

| Feature | V1 | V2 |
|---------|----|----|
| Checklist items | Text items (e.g., "Bathroom cleaned", "Linens replaced") | Items with photo proof requirement |
| Default template | Platform provides a default template; tenant can customize | AI-generated checklists based on property type |
| Per-property override | Yes — property-level template overrides tenant-level | Room-type-level templates |
| Completion tracking | Staff checks off items; percentage completion stored | Photo validation per item |
| Damage flag | Staff can flag damage on any item → creates linked `Incident` | AI damage detection from photos |

> **OPS-HK-005**: `HousekeepingChecklistTemplate` stores the template. `HousekeepingTask.ChecklistProgressJson` stores runtime completion state as JSON array of `{itemId, checked, note?, damageFlag?}`.

### 2.6 Room/Unit Status

Derived from housekeeping task state and booking state:

| Room Status | Condition |
|-------------|-----------|
| `DIRTY` | Checkout completed, no completed cleaning task since last checkout |
| `CLEANING` | Housekeeping task in `ASSIGNED` or `IN_PROGRESS` |
| `INSPECTING` | Housekeeping task in `QC_PENDING` |
| `READY` | Latest cleaning task `COMPLETED` and no active booking currently checked in |
| `OCCUPIED` | Active booking with `CheckedInAtUtc` set and `CheckedOutAtUtc` null |
| `BLOCKED` | Maintenance ticket with severity `HIGH` or `P0` open for this listing |

> **OPS-HK-006**: Room status is a **computed value**, not stored. Derived at query time from latest `HousekeepingTask` status and `Booking` state for the listing. A dedicated endpoint `/api/listings/{id}/room-status` returns the current status.

### 2.7 UI Requirements

#### 2.7.1 Today Board (Kanban)

A single-screen Kanban board showing all housekeeping tasks for today, grouped by status columns:

| Column | Content |
|--------|---------|
| **To Do** | `AUTO_CREATED` + `ASSIGNED` tasks |
| **In Progress** | `IN_PROGRESS` tasks |
| **QC** | `QC_PENDING` tasks |
| **Done** | `COMPLETED` tasks (today only) |
| **Reopened** | `REOPENED` tasks |

**Features**:
- Filter by property (for multi-property tenants)
- Color-coded urgency: red = due within 1 hour, yellow = due within 3 hours, green = comfortable
- Tap card → task detail with checklist, assigned staff, notes
- Drag-and-drop to reassign (desktop); tap "Assign" button (mobile)
- Pull-to-refresh on mobile

> **OPS-HK-007**: The Today Board is the **default landing page** for users with `housekeeping` or `manager` role.

#### 2.7.2 Room Status Grid

Grid view of all listings for a property showing current room status as colored tiles:

| Color | Status |
|-------|--------|
| 🔴 Red | `DIRTY` |
| 🟡 Yellow | `CLEANING` / `INSPECTING` |
| 🟢 Green | `READY` |
| 🔵 Blue | `OCCUPIED` |
| ⚫ Grey | `BLOCKED` |

Tap tile → shows current task, next booking, assigned staff.

#### 2.7.3 Quick Actions (Mobile-Friendly)

| Action | Access | Effect |
|--------|--------|--------|
| "Start Cleaning" | Assigned staff | Status → `IN_PROGRESS` |
| "Mark Done" | Staff doing cleaning | Status → `QC_PENDING` or `COMPLETED` |
| "Approve" / "Reject" | Manager/inspector | `QC_PENDING` → `COMPLETED` or `REOPENED` |
| "Flag Damage" | Any staff | Creates linked `Incident` with pre-filled listing |
| "Create Task" | Manager | Manually create cleaning task for any listing |
| "Reassign" | Manager | Change assigned staff |

> **OPS-HK-008**: All quick actions are single-tap operations. No multi-step forms for status transitions.

### 2.8 Automation Integration

| Trigger | Action | Mechanism |
|---------|--------|-----------|
| `stay.checked_out` event | Auto-create `TURNOVER` housekeeping task | `AutomationRule` with `TriggerEventType = 'stay.checked_out'`, `ActionType = 'CREATE_HOUSEKEEPING_TASK'` |
| `booking.confirmed` event | Schedule mid-stay cleaning tasks (if stay > N days) | `AutomationSchedule` entries created for each mid-stay clean date |
| Housekeeping task overdue | Escalate to property manager | `EscalationSweepJob` checks `DueAtUtc` on `HousekeepingTask` |
| Housekeeping task `COMPLETED` | Update room status, notify front desk | `OutboxMessage` with `ops.housekeeping.completed` |
| Housekeeping damage flag | Create `Incident` and notify manager | Inline on task completion → `OutboxMessage` with `ops.incident.created` |

---

## 3. Maintenance / Work Orders (V1)

### 3.1 Ticket Types

| Type Code | Display Name | Examples |
|-----------|-------------|----------|
| `PLUMBING` | Plumbing | Leaking tap, clogged drain, water heater failure |
| `ELECTRICAL` | Electrical | Power outlet failure, light fixture, MCB trip |
| `HVAC` | HVAC / Cooling | AC not cooling, fan noise, geyser issue |
| `FURNITURE` | Furniture | Broken bed frame, damaged table, wardrobe door |
| `INTERNET` | Internet / IT | WiFi down, router reset, smart lock battery |
| `APPLIANCES` | Appliances | Washing machine, refrigerator, microwave |
| `STRUCTURAL` | Structural | Wall crack, ceiling leak, door lock, window |
| `OTHER` | Other | Pest control, general handyman |

### 3.2 Severity Levels

| Severity | Code | SLA (Resolution) | SLA (First Response) | Examples |
|----------|------|-------------------|---------------------|----------|
| **Low** | `LOW` | 72 hours | 24 hours | Cosmetic issue, minor scratch |
| **Medium** | `MEDIUM` | 24 hours | 4 hours | Non-critical appliance failure, AC not optimal |
| **High** | `HIGH` | 8 hours | 1 hour | No hot water, WiFi down, door lock failure |
| **P0** | `P0` | 2 hours | 15 minutes | Water flooding, electrical hazard, guest locked out, safety issue |

> **OPS-MT-001**: SLA timers are measured from `CreatedAtUtc`. First-response means ticket moves out of `CREATED` status. Resolution means ticket reaches `RESOLVED` status.

### 3.3 Ticket Lifecycle

```
CREATED ──► ASSIGNED ──► IN_PROGRESS ──► WAITING_VENDOR ──► RESOLVED ──► VERIFIED ──► CLOSED
    │            │              │                                  │            │
    │            │              │                                  │            ▼
    │            │              ▼                                  │       REOPENED → IN_PROGRESS
    │            ▼         CANCELLED                               ▼
    │       CANCELLED                                         CLOSED (auto after 48h if not verified)
    ▼
 CANCELLED
```

| Status | Description | Who Transitions |
|--------|-------------|-----------------|
| `CREATED` | Ticket raised (manually or from guest complaint) | System or staff |
| `ASSIGNED` | Assigned to maintenance staff or vendor | Manager |
| `IN_PROGRESS` | Work underway | Assigned staff |
| `WAITING_VENDOR` | External vendor required; waiting for vendor visit | Staff/Manager |
| `RESOLVED` | Fix applied, pending verification | Staff |
| `VERIFIED` | Manager/guest confirms fix is satisfactory | Manager |
| `CLOSED` | Terminal state. Auto-close 48h after `RESOLVED` if not explicitly verified | System or Manager |
| `REOPENED` | Issue recurred after verification | Manager |
| `CANCELLED` | Invalid or duplicate ticket | Manager |

> **OPS-MT-002**: Every status transition MUST be recorded in `AuditLog` with `EntityType = 'MaintenanceTicket'`.

### 3.4 Ticket Linking

| Link | Required? | Purpose |
|------|-----------|---------|
| **Property** | Yes (via `Listing.PropertyId`) | Which property |
| **Listing** | Yes | Which room/unit |
| **Booking** | Optional | If guest reported the issue |
| **Guest** | Optional (via Booking) | For guest-reported issues, communication |
| **Vendor** | Optional | If external vendor assigned |
| **Expense** | Optional | Track repair cost |
| **HousekeepingTask** | Optional | If discovered during cleaning |

### 3.5 Cost Tracking

| Field | Type | Description |
|-------|------|-------------|
| `EstimatedCost` | `decimal(10,2)?` | Initial estimate (INR) |
| `ActualCost` | `decimal(10,2)?` | Final cost after resolution |
| `VendorId` | `int?` | Linked vendor who performed the work |
| `ExpenseId` | `int?` | Linked expense record for accounting |
| `InvoiceUrl` | `nvarchar(500)?` | V2: URL to uploaded invoice image |

> **OPS-MT-003**: Cost fields are informational in V1. No approval workflows. Invoice upload deferred to V2.

### 3.6 SLA and Escalation

| Rule ID | Trigger | Action |
|---------|---------|--------|
| **OPS-MT-SLA-001** | P0 ticket not assigned within 15 min | Alert tenant owner + Atlas Admin via WhatsApp |
| **OPS-MT-SLA-002** | HIGH ticket not assigned within 1 hour | Alert property manager |
| **OPS-MT-SLA-003** | Any ticket exceeds resolution SLA | Escalate one level (Staff → Manager → Owner → Atlas Admin) |
| **OPS-MT-SLA-004** | Ticket in `WAITING_VENDOR` > 24 hours | Remind manager and flag for follow-up |
| **OPS-MT-SLA-005** | Resolved ticket not verified within 48 hours | Auto-close with `ClosedReason = 'AUTO_VERIFIED'` |

### 3.7 Maintenance Automation

| Trigger | Action |
|---------|--------|
| Guest complaint via WhatsApp (V2) | Auto-create maintenance ticket |
| Housekeeping damage flag | Create `MEDIUM` maintenance ticket linked to listing |
| Scheduled preventive maintenance (V2) | Create ticket from `AutomationSchedule` |
| P0 ticket created | Immediate WhatsApp alert to all managers for property |

---

## 4. Inventory & Consumables (V1)

### 4.1 Inventory Categories

| Category Code | Display Name | Examples | Unit of Measure |
|---------------|-------------|----------|----------------|
| `LINEN` | Linen | Bedsheets, pillow covers, towels, bathrobes | `PIECE` |
| `TOILETRY` | Toiletries | Soap, shampoo, conditioner, toothpaste kit | `PIECE` or `PACK` |
| `CLEANING` | Cleaning Supplies | Floor cleaner, toilet cleaner, disinfectant, broom | `BOTTLE`, `PIECE`, `LITRE` |
| `PANTRY` | Pantry Items (optional) | Tea, coffee, sugar, water bottles, snacks | `PIECE`, `PACK`, `KG` |
| `AMENITY` | Guest Amenities | Slippers, welcome kit, adapter, umbrella | `PIECE` |
| `MAINTENANCE` | Maintenance Supplies | Light bulbs, batteries, fuses, plumbing parts | `PIECE` |

### 4.2 Unit of Measure Rules

| Code | Display | Use With |
|------|---------|----------|
| `PIECE` | Piece(s) | Individual items (towels, soap bars, bulbs) |
| `PACK` | Pack(s) | Pre-packaged bundles |
| `BOTTLE` | Bottle(s) | Liquid containers |
| `LITRE` | Litre(s) | Bulk liquids |
| `KG` | Kilogram(s) | Bulk dry goods |
| `SET` | Set(s) | Matched sets (bedding set, welcome kit) |

### 4.3 Stock Management Features

#### 4.3.1 Stock In/Out Entries

| Transaction Type | Code | Triggered By |
|-----------------|------|-------------|
| **Purchase** | `PURCHASE` | Manual entry when stock received from vendor |
| **Check-in Deduction** | `CHECKIN_DEDUCT` | Auto-deduct on guest check-in (template-based) |
| **Manual Issue** | `MANUAL_ISSUE` | Staff takes items for ad-hoc use |
| **Return** | `RETURN` | Items returned to stock (e.g., unused amenities) |
| **Damaged/Write-off** | `WRITE_OFF` | Items damaged or expired |
| **Transfer** | `TRANSFER` | Moved between properties (inter-property) |
| **Adjustment** | `ADJUSTMENT` | Stock count correction |

> **OPS-INV-001**: Every transaction creates an `InventoryTransaction` record. Running stock balance is computed as `SUM(Quantity) WHERE TransactionType IN ('PURCHASE', 'RETURN', 'ADJUSTMENT') - SUM(Quantity) WHERE TransactionType IN ('CHECKIN_DEDUCT', 'MANUAL_ISSUE', 'WRITE_OFF', 'TRANSFER')` per `InventoryItemId`.

#### 4.3.2 Per-Checkin Auto-Deduct (Template-Based)

Each property defines a **consumption template** — a list of items and quantities consumed per guest check-in:

| Example Template: "Standard Room Check-in" | |
|---------------------------------------------|---|
| Bedsheet set × 1 | |
| Bath towel × 2 | |
| Hand towel × 2 | |
| Soap bar × 2 | |
| Shampoo sachet × 2 | |
| Water bottle × 2 | |
| Welcome kit × 1 | |

> **OPS-INV-002**: When a `stay.checked_in` event fires, the system looks up the consumption template for the listing's property, multiplies by guest count if applicable, and creates `CHECKIN_DEDUCT` transactions. If stock is insufficient, the deduction still occurs (allowing negative stock) but a `ops.inventory.low_stock` alert is raised.

#### 4.3.3 Stock Level Model

| Level | Meaning |
|-------|---------|
| **Current Stock** | Computed from transaction history |
| **Minimum Stock (reorder point)** | Configurable per item per property. Below this → alert |
| **Par Level** | Ideal stock level. `Par - Current = Suggested Purchase Quantity` |

#### 4.3.4 Store Hierarchy

| Level | Description |
|-------|-------------|
| **Property Store** | Central store for the property. Default for all stock entries |
| **Room Store** | V2: Per-room mini-bar or in-room consumable tracking |

V1 tracks stock at the **property level** only.

### 4.4 Alerts

| Alert | Condition | Channel | Recipient |
|-------|-----------|---------|-----------|
| **Low Stock** | Current stock ≤ minimum stock for any item | WhatsApp + Dashboard | Property Manager |
| **Zero Stock** | Current stock ≤ 0 | WhatsApp + Dashboard | Property Manager + Tenant Owner |
| **High Consumption** | V2: Daily consumption exceeds 2× average | Dashboard | Manager |

> **OPS-INV-003**: Low-stock check runs as a daily batch job (`InventoryAlertJob`) at 08:00 local time. Also triggered inline on every `CHECKIN_DEDUCT` transaction.

### 4.5 Procurement Suggestions (Lean V1)

When stock falls below minimum, the system generates a **procurement suggestion**:

```
Property: Sunset Villa
Item: Bath Towel
Current Stock: 3
Minimum Stock: 10
Par Level: 20
Suggested Purchase: 17 pieces
Preferred Vendor: CleanCo Supplies (if linked)
Last Purchase Price: ₹85/piece
Estimated Cost: ₹1,445
```

This is a **read-only suggestion** in V1 — no auto-purchase-order. Staff can create an `Expense` entry from the suggestion.

---

## 5. Vendors & Purchase Tracking (V1)

### 5.1 Vendor Directory

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | `int` (PK) | Auto | |
| `TenantId` | `int` | Yes | Tenant scope |
| `Name` | `nvarchar(200)` | Yes | Vendor/supplier name |
| `ContactPerson` | `nvarchar(100)` | No | Primary contact name |
| `Phone` | `varchar(20)` | Yes | Primary phone |
| `Email` | `varchar(200)` | No | Email address |
| `Address` | `nvarchar(500)` | No | Physical address |
| `Category` | `varchar(30)` | Yes | `CLEANING`, `LINEN`, `MAINTENANCE`, `PANTRY`, `GENERAL` |
| `GSTIN` | `varchar(15)` | No | GST Identification Number (India) |
| `PAN` | `varchar(10)` | No | PAN for TDS purposes (V2) |
| `BankAccountInfo` | `nvarchar(500)` | No | Bank details for payment (encrypted at rest) |
| `PaymentTerms` | `varchar(50)` | No | e.g., "Net 30", "Immediate", "COD" |
| `IsActive` | `bit` | Yes | Soft-delete / deactivate |
| `Notes` | `nvarchar(1000)` | No | Free-form notes |
| `CreatedAtUtc` | `datetime2` | Yes | |
| `UpdatedAtUtc` | `datetime2` | Yes | |

> **OPS-VND-001**: Vendor `BankAccountInfo` MUST be stored encrypted. In V1, AES-256 column-level encryption via application layer. Displayed masked in UI (last 4 digits only).

### 5.2 Expense Entries

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | `int` (PK) | Auto | |
| `TenantId` | `int` | Yes | |
| `PropertyId` | `int` | Yes | Which property |
| `VendorId` | `int?` | No | Linked vendor |
| `Category` | `varchar(30)` | Yes | `LINEN`, `TOILETRY`, `CLEANING`, `MAINTENANCE`, `UTILITY`, `SALARY`, `OTHER` |
| `Description` | `nvarchar(500)` | Yes | What was purchased/spent |
| `Amount` | `decimal(10,2)` | Yes | Total amount (INR) |
| `TaxAmount` | `decimal(10,2)` | No | GST or other tax |
| `PaymentStatus` | `varchar(10)` | Yes | `UNPAID`, `PARTIAL`, `PAID` |
| `AmountPaid` | `decimal(10,2)` | Yes | Amount paid so far |
| `PaymentMethod` | `varchar(20)` | No | `CASH`, `UPI`, `BANK_TRANSFER`, `CARD` |
| `PaymentDate` | `date?` | No | When payment was made |
| `InvoiceNumber` | `varchar(50)` | No | Vendor invoice number |
| `InvoiceUrl` | `nvarchar(500)` | No | V2: Uploaded invoice image URL |
| `TDSApplicable` | `bit` | No | V2: Whether TDS is deducted |
| `ExpenseDate` | `date` | Yes | Date of expense |
| `MaintenanceTicketId` | `int?` | No | Linked maintenance ticket |
| `Notes` | `nvarchar(500)` | No | |
| `CreatedByUserId` | `int` | Yes | Who created the entry |
| `CreatedAtUtc` | `datetime2` | Yes | |
| `UpdatedAtUtc` | `datetime2` | Yes | |

> **OPS-VND-002**: Expense entries are the **single source of truth** for property-level cost tracking. Maintenance ticket costs flow through expenses.

### 5.3 Payment Status Lifecycle

```
UNPAID ──► PARTIAL ──► PAID
  │            │
  └────────────┘ (direct to PAID also allowed)
```

> **OPS-VND-003**: Payment status transitions must be logged in `AuditLog` with old and new amounts.

### 5.4 Monthly Cost Report

Auto-generated monthly report per property:

| Section | Content |
|---------|---------|
| **Summary** | Total expenses, total by category, total by vendor |
| **Category Breakdown** | Pie chart data: Linen ₹X, Cleaning ₹Y, Maintenance ₹Z, etc. |
| **Vendor Breakdown** | Top vendors by spend |
| **Unpaid Liabilities** | Outstanding `UNPAID` + `PARTIAL` expenses |
| **Month-over-Month** | Comparison with previous month |
| **Per-Night Cost** | Total ops cost / total occupied nights = cost per night |

> **OPS-VND-004**: Report data is computed from `Expense` table with date range filter. No separate reporting table in V1 — queries run against OLTP with read-committed snapshot isolation.

### 5.5 India Context

| Feature | V1 | V2 |
|---------|----|----|
| **GSTIN field** | Optional on Vendor | GST filing integration |
| **Invoice upload** | Not supported | Blob storage with OCR extraction |
| **TDS flag** | Not supported | TDS calculation and reporting |
| **INR formatting** | All amounts in INR with ₹ symbol, Indian number formatting (1,00,000) | Multi-currency |

---

## 6. Incidents & Damage Claims (V1)

### 6.1 Incident Model (Enhanced)

The existing `Incident` model is minimal (Id, ListingId, BookingId, Description, ActionTaken, Status, CreatedBy, CreatedOn). V1 ops extends this significantly.

> **OPS-INC-001**: The existing `Incident` entity will be **migrated** to the new schema via an EF Core migration. Existing data is preserved; new columns are nullable or have defaults.

**New Incident schema:**

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `Id` | `int` (PK) | Auto | Existing |
| `TenantId` | `int` | Yes | **NEW** — makes Incident tenant-scoped (`ITenantOwnedEntity`) |
| `ListingId` | `int` | Yes | Existing |
| `PropertyId` | `int` | Yes | **NEW** — denormalized for faster queries |
| `BookingId` | `int?` | No | Existing |
| `GuestId` | `int?` | No | **NEW** — direct guest link |
| `IncidentType` | `varchar(30)` | Yes | **NEW** — `DAMAGE`, `COMPLAINT`, `SAFETY`, `THEFT`, `NOISE`, `OTHER` |
| `Severity` | `varchar(10)` | Yes | **NEW** — `LOW`, `MEDIUM`, `HIGH`, `CRITICAL` |
| `Description` | `nvarchar(2000)` | Yes | Existing (expanded length) |
| `ActionTaken` | `nvarchar(2000)` | No | Existing (made optional for new incidents) |
| `EstimatedCost` | `decimal(10,2)?` | No | **NEW** — estimated damage/resolution cost |
| `ActualCost` | `decimal(10,2)?` | No | **NEW** — final cost |
| `Status` | `varchar(20)` | Yes | Existing (expanded values) |
| `ResolutionNotes` | `nvarchar(2000)` | No | **NEW** |
| `ChargesRecoveryNote` | `nvarchar(500)` | No | **NEW** — manual note on whether charges recovered from guest |
| `PhotoUrls` | `nvarchar(max)` | No | **V2** — JSON array of photo URLs |
| `LinkedHousekeepingTaskId` | `int?` | No | **NEW** — if discovered during cleaning |
| `LinkedMaintenanceTicketId` | `int?` | No | **NEW** — if maintenance ticket was created |
| `CreatedBy` | `nvarchar(100)` | Yes | Existing |
| `CreatedByUserId` | `int?` | No | **NEW** — foreign key to `User` |
| `CreatedOn` | `datetime` | Yes | Existing |
| `ResolvedAtUtc` | `datetime2?` | No | **NEW** |
| `UpdatedAtUtc` | `datetime2` | Yes | **NEW** |

**Incident statuses:**

| Status | Description |
|--------|-------------|
| `OPEN` | Incident reported |
| `INVESTIGATING` | Under review |
| `ACTION_TAKEN` | Resolution action performed |
| `RESOLVED` | Incident resolved |
| `CLOSED` | Final state (with or without recovery) |

### 6.2 Damage Claims

V1 is manual:
- Staff or manager creates an incident with `IncidentType = 'DAMAGE'` and `EstimatedCost`.
- `ChargesRecoveryNote` records whether guest was charged (e.g., "Deducted ₹2,000 from security deposit").
- No automated deduction from guest in V1.

V2 will support:
- Photo evidence upload and AI assessment.
- Security deposit auto-hold and auto-deduction.
- Insurance claim draft generation.
- Guest dispute workflow.

### 6.3 Trust/Quality Impact

> **OPS-INC-002**: Incident frequency per property per month is a factor in the **property quality score** (ref: RA-DATA-001 `DailyTrustScore`). High incident frequency lowers the quality component of TrustScore.

Weighting (configurable):
- `DAMAGE` incidents: -2 points per incident (capped at -10/month)
- `SAFETY` incidents: -5 points per incident (capped at -15/month)
- `COMPLAINT` incidents: -1 point per incident (capped at -5/month)
- Other types: -0.5 points (capped at -3/month)

---

## 7. Staff Management (Lean V1)

### 7.1 Staff Profiles

The existing `User` entity (Id, TenantId, Name, Phone, Email, PasswordHash, Role) serves as the staff profile. No separate `Staff` entity in V1.

**Ops-relevant fields on User (new):**

| Field | Type | Description |
|-------|------|-------------|
| `AssignedPropertyIds` | `nvarchar(500)` | **NEW** — Comma-separated property IDs this staff member works at. NULL = all properties |
| `IsFieldStaff` | `bit` | **NEW** — True for housekeeping/maintenance staff who use mobile UI |
| `MaxDailyTasks` | `int?` | **NEW** — Maximum tasks assignable per day (V1: informational, V2: enforced) |

> **OPS-STF-001**: V1 extends the `User` model with ops fields. A separate `StaffProfile` entity may be introduced in V2 if the `User` model grows too large.

### 7.2 Roles and Permissions

| Role | Code | Permissions |
|------|------|------------|
| **Housekeeping** | `housekeeping` | View assigned tasks, update task status, flag damage, view own schedule |
| **Maintenance** | `maintenance` | View assigned tickets, update ticket status, log cost, view own schedule |
| **Front Desk** | `frontdesk` | View room status, check-in/out guests, create incidents, view inventory |
| **Manager** | `manager` | All of above + assign tasks/tickets, approve QC, manage inventory, create expenses, view reports |
| **Owner** | `owner` | All of above + manage staff, configure SLAs, view financial reports, manage vendors |

> **OPS-STF-002**: Permissions are enforced via role checks in API endpoints. V1 uses simple role-string matching. V2 may introduce a granular permission matrix.

### 7.3 Workload Distribution (Simple V1)

| Rule | Description |
|------|-------------|
| **Round-robin** | New tasks assigned to the staff member with the fewest active (non-completed) tasks today |
| **Property affinity** | Only assign to staff whose `AssignedPropertyIds` includes the task's property |
| **Capacity check** | If `MaxDailyTasks` is set and reached, skip this staff member in rotation |
| **Fallback** | If no eligible staff, leave unassigned and alert manager |

> **OPS-STF-003**: Workload distribution runs at task creation time and is not re-balanced. Manual reassignment is always possible.

### 7.4 Future V2 Features (Out of Scope)

- **Attendance tracking**: Clock-in/clock-out with geo-fencing.
- **Shift scheduling**: Drag-and-drop shift calendar.
- **Performance scoring**: Task completion rate, SLA compliance, guest ratings.
- **Staff app**: Dedicated PWA for field staff with offline capability.

---

## 8. Reporting Requirements (Ops)

### 8.1 Tenant View (Property Manager / Owner)

| Report | Data Source | Refresh | Format |
|--------|-----------|---------|--------|
| **Cleaning SLA Compliance** | `HousekeepingTask` — % completed before `DueAtUtc` | Daily | % metric + trend chart |
| **Average Turnover Time** | `HousekeepingTask` — avg(`CompletedAtUtc` - `CreatedAtUtc`) for `TURNOVER` type | Daily | Minutes metric + trend |
| **Maintenance Ticket Aging** | `MaintenanceTicket` — open tickets grouped by age bucket (0-24h, 24-48h, 48-72h, 72h+) | Real-time | Bar chart |
| **Maintenance SLA Compliance** | `MaintenanceTicket` — % resolved within severity SLA | Weekly | % metric |
| **Inventory Consumption** | `InventoryTransaction` — total consumed per category per month | Monthly | Table + bar chart |
| **Low Stock Items** | `InventoryItem` — items where current stock ≤ minimum | Real-time | Alert list |
| **Monthly Ops Cost** | `Expense` — total by category by property | Monthly | Summary + pie chart |
| **Cost Per Night** | `Expense` total / occupied nights from `Booking` | Monthly | ₹ metric + trend |
| **Incident Summary** | `Incident` — count by type and severity | Monthly | Table |
| **Staff Utilization** | `HousekeepingTask` + `MaintenanceTicket` — tasks per staff member | Weekly | Table |

### 8.2 Management View (Atlas Internal)

| Report | Description | Access |
|--------|-------------|--------|
| **Cross-Tenant Ops Metrics** | Aggregated SLA compliance, avg turnover time, ticket volume across all tenants | Atlas Admin only |
| **Ops Maturity Score** | Per-tenant score based on: SLA compliance, incident rate, inventory management usage | Atlas Admin only |
| **Platform Ops Load** | Total tasks/tickets created per day, resolution rates, escalation frequency | Atlas Admin only |
| **Vendor Concentration** | Which vendors serve multiple tenants (marketplace opportunity signal) | Atlas Admin only |

> **OPS-RPT-001**: Management reports use anonymized/aggregated data. Individual tenant expense details are NOT visible to Atlas Admin. Only aggregate metrics are shown.

---

## 9. Data Model & APIs (LLR)

### 9.1 New Entities

#### 9.1.1 `HousekeepingTask`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | Tenant scope |
| `ListingId` | `int` (FK → Listing) | No | Which room/unit |
| `PropertyId` | `int` (FK → Property) | No | Denormalized for faster queries |
| `BookingId` | `int?` (FK → Booking) | Yes | Triggering booking (checkout) |
| `NextBookingId` | `int?` (FK → Booking) | Yes | Next booking (determines SLA deadline) |
| `CleaningType` | `varchar(15)` | No | `TURNOVER`, `MID_STAY`, `DEEP_CLEAN`, `TOUCH_UP` |
| `Status` | `varchar(15)` | No | See §2.1 lifecycle |
| `AssignedToUserId` | `int?` (FK → User) | Yes | Assigned staff member |
| `AssignedAtUtc` | `datetime2?` | Yes | When assigned |
| `StartedAtUtc` | `datetime2?` | Yes | When staff started |
| `CompletedAtUtc` | `datetime2?` | Yes | When marked complete |
| `DueAtUtc` | `datetime2` | No | SLA deadline (next check-in time or fallback) |
| `ChecklistTemplateId` | `int?` (FK) | Yes | Which checklist template applies |
| `ChecklistProgressJson` | `nvarchar(max)` | Yes | Runtime checklist state (JSON) |
| `DamageFlag` | `bit` | No | Default `0`; set if damage discovered |
| `LinkedIncidentId` | `int?` (FK → Incident) | Yes | Auto-created incident if damage flagged |
| `Notes` | `nvarchar(1000)` | Yes | Staff notes |
| `QcByUserId` | `int?` (FK → User) | Yes | Who performed QC |
| `QcAtUtc` | `datetime2?` | Yes | When QC was performed |
| `QcNotes` | `nvarchar(500)` | Yes | QC rejection reason or approval note |
| `EscalationLevel` | `int` | No | Default `0`; incremented on each escalation |
| `CreatedAtUtc` | `datetime2` | No | |
| `UpdatedAtUtc` | `datetime2` | No | |

**Indexes:**
- `IX_HousekeepingTask_TenantId_Status_DueAtUtc` on `(TenantId, Status, DueAtUtc)` — Today Board queries
- `IX_HousekeepingTask_ListingId_Status` on `(ListingId, Status)` — Room status derivation
- `IX_HousekeepingTask_AssignedToUserId` on `(AssignedToUserId)` — Staff workload queries

#### 9.1.2 `HousekeepingChecklistTemplate`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | |
| `PropertyId` | `int?` (FK → Property) | Yes | NULL = tenant-wide default; set = property-specific override |
| `Name` | `nvarchar(100)` | No | e.g., "Standard Room Turnover" |
| `CleaningType` | `varchar(15)` | No | Which cleaning type this template applies to |
| `ItemsJson` | `nvarchar(max)` | No | JSON array of `{id, label, category?, required?}` |
| `IsActive` | `bit` | No | |
| `CreatedAtUtc` | `datetime2` | No | |
| `UpdatedAtUtc` | `datetime2` | No | |

**Template resolution priority:**
1. Property-specific template for the cleaning type → use it
2. Tenant-wide template for the cleaning type → use it
3. No template → task created without checklist

#### 9.1.3 `MaintenanceTicket`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | |
| `ListingId` | `int` (FK → Listing) | No | |
| `PropertyId` | `int` (FK → Property) | No | Denormalized |
| `BookingId` | `int?` (FK → Booking) | Yes | If guest-reported |
| `TicketType` | `varchar(20)` | No | See §3.1 |
| `Severity` | `varchar(10)` | No | `LOW`, `MEDIUM`, `HIGH`, `P0` |
| `Title` | `nvarchar(200)` | No | Short description |
| `Description` | `nvarchar(2000)` | No | Detailed description |
| `Status` | `varchar(20)` | No | See §3.3 lifecycle |
| `AssignedToUserId` | `int?` (FK → User) | Yes | Internal staff |
| `VendorId` | `int?` (FK → Vendor) | Yes | External vendor |
| `EstimatedCost` | `decimal(10,2)?` | Yes | |
| `ActualCost` | `decimal(10,2)?` | Yes | |
| `ExpenseId` | `int?` (FK → Expense) | Yes | Linked expense record |
| `InvoiceUrl` | `nvarchar(500)` | Yes | V2 |
| `FirstResponseAtUtc` | `datetime2?` | Yes | When ticket left `CREATED` status |
| `ResolvedAtUtc` | `datetime2?` | Yes | |
| `ClosedAtUtc` | `datetime2?` | Yes | |
| `ClosedReason` | `varchar(20)` | Yes | `VERIFIED`, `AUTO_VERIFIED`, `CANCELLED`, `DUPLICATE` |
| `EscalationLevel` | `int` | No | Default `0` |
| `LinkedHousekeepingTaskId` | `int?` | Yes | If discovered during cleaning |
| `Notes` | `nvarchar(2000)` | Yes | |
| `CreatedByUserId` | `int` | No | |
| `CreatedAtUtc` | `datetime2` | No | |
| `UpdatedAtUtc` | `datetime2` | No | |

**Indexes:**
- `IX_MaintenanceTicket_TenantId_Status` on `(TenantId, Status)` — Active ticket queries
- `IX_MaintenanceTicket_PropertyId_Status` on `(PropertyId, Status)` — Property dashboard
- `IX_MaintenanceTicket_Severity_Status` on `(Severity, Status)` — P0/HIGH alert queries

#### 9.1.4 `InventoryItem`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | |
| `PropertyId` | `int` (FK → Property) | No | Stock tracked per property |
| `Category` | `varchar(20)` | No | See §4.1 |
| `Name` | `nvarchar(200)` | No | e.g., "Bath Towel - White" |
| `SKU` | `varchar(50)` | Yes | Optional SKU/part number |
| `UnitOfMeasure` | `varchar(10)` | No | See §4.2 |
| `MinimumStock` | `int` | No | Reorder point |
| `ParLevel` | `int` | No | Ideal stock level |
| `CurrentStock` | `int` | No | Denormalized running balance (updated on each transaction) |
| `PreferredVendorId` | `int?` (FK → Vendor) | Yes | Default vendor for procurement suggestions |
| `LastPurchasePrice` | `decimal(10,2)?` | Yes | Most recent purchase price per unit |
| `IsActive` | `bit` | No | |
| `CreatedAtUtc` | `datetime2` | No | |
| `UpdatedAtUtc` | `datetime2` | No | |

**Indexes:**
- `IX_InventoryItem_TenantId_PropertyId_Category` on `(TenantId, PropertyId, Category)` — Filtered queries
- `IX_InventoryItem_TenantId_CurrentStock` on `(TenantId, CurrentStock)` WHERE `CurrentStock <= MinimumStock` — Low-stock alerts (filtered index)

> **OPS-INV-004**: `CurrentStock` is a denormalized field updated within the same transaction as `InventoryTransaction` insert. A nightly reconciliation job verifies it matches the computed sum from transactions and logs discrepancies.

#### 9.1.5 `InventoryTransaction`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `long` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | |
| `InventoryItemId` | `int` (FK → InventoryItem) | No | |
| `TransactionType` | `varchar(20)` | No | See §4.3.1 |
| `Quantity` | `int` | No | Positive for additions, negative for deductions |
| `BookingId` | `int?` (FK → Booking) | Yes | For `CHECKIN_DEDUCT` |
| `VendorId` | `int?` (FK → Vendor) | Yes | For `PURCHASE` |
| `UnitPrice` | `decimal(10,2)?` | Yes | Price per unit (for purchases) |
| `Notes` | `nvarchar(500)` | Yes | |
| `CreatedByUserId` | `int` | No | |
| `CreatedAtUtc` | `datetime2` | No | |

**Indexes:**
- `IX_InventoryTransaction_InventoryItemId_CreatedAtUtc` on `(InventoryItemId, CreatedAtUtc)` — Transaction history
- `IX_InventoryTransaction_TenantId_TransactionType_CreatedAtUtc` on `(TenantId, TransactionType, CreatedAtUtc)` — Consumption reports

#### 9.1.6 `Vendor`

See §5.1 for full schema. Additional:

**Indexes:**
- `IX_Vendor_TenantId_Category` on `(TenantId, Category)` — Vendor lookup by category
- `IX_Vendor_TenantId_IsActive` on `(TenantId, IsActive)` — Active vendor list

#### 9.1.7 `Expense`

See §5.2 for full schema. Additional:

**Indexes:**
- `IX_Expense_TenantId_PropertyId_ExpenseDate` on `(TenantId, PropertyId, ExpenseDate)` — Monthly reports
- `IX_Expense_VendorId` on `(VendorId)` — Vendor spend analysis
- `IX_Expense_PaymentStatus` on `(TenantId, PaymentStatus)` WHERE `PaymentStatus != 'PAID'` — Outstanding payments

#### 9.1.8 `Incident` (Enhanced)

See §6.1 for full schema. Migration from existing `Incident` table.

**New Indexes:**
- `IX_Incident_TenantId_PropertyId_IncidentType` on `(TenantId, PropertyId, IncidentType)` — Property incident dashboard
- `IX_Incident_TenantId_Status` on `(TenantId, Status)` — Open incidents

#### 9.1.9 `InventoryConsumptionTemplate`

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `Id` | `int` (PK, identity) | No | |
| `TenantId` | `int` (FK → Tenant) | No | |
| `PropertyId` | `int?` (FK → Property) | Yes | NULL = tenant-wide; set = property-specific |
| `Name` | `nvarchar(100)` | No | e.g., "Standard Room Check-in" |
| `ItemsJson` | `nvarchar(max)` | No | JSON array of `{inventoryItemId, quantity, perGuest?}` |
| `IsActive` | `bit` | No | |
| `CreatedAtUtc` | `datetime2` | No | |
| `UpdatedAtUtc` | `datetime2` | No | |

### 9.2 Entity Registration

All new entities MUST be registered in `AppDbContext.OnModelCreating()`:

```
modelBuilder.Entity<HousekeepingTask>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<MaintenanceTicket>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<InventoryItem>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<InventoryTransaction>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<Vendor>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<Expense>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<InventoryConsumptionTemplate>().HasQueryFilter(e => e.TenantId == _tenantId);
modelBuilder.Entity<HousekeepingChecklistTemplate>().HasQueryFilter(e => e.TenantId == _tenantId);
// Incident already registered; update query filter to include TenantId
```

### 9.3 API Endpoints

#### 9.3.1 Housekeeping

| Method | Endpoint | Permission | Description |
|--------|----------|-----------|-------------|
| `GET` | `/api/housekeeping/today` | `housekeeping`, `manager`, `owner` | Today Board tasks (filtered by property if specified) |
| `GET` | `/api/housekeeping/{id}` | `housekeeping`, `manager`, `owner` | Task detail with checklist |
| `POST` | `/api/housekeeping` | `manager`, `owner` | Manually create a task |
| `PATCH` | `/api/housekeeping/{id}/status` | `housekeeping`, `manager` | Update status (validates transition) |
| `PATCH` | `/api/housekeeping/{id}/assign` | `manager`, `owner` | Assign/reassign staff |
| `PATCH` | `/api/housekeeping/{id}/checklist` | `housekeeping`, `manager` | Update checklist progress |
| `POST` | `/api/housekeeping/{id}/damage` | `housekeeping`, `manager` | Flag damage → creates Incident |
| `GET` | `/api/listings/{id}/room-status` | `frontdesk`, `housekeeping`, `manager`, `owner` | Computed room status |
| `GET` | `/api/properties/{id}/room-status` | `frontdesk`, `housekeeping`, `manager`, `owner` | All listings' room status for a property |
| `GET` | `/api/housekeeping/checklist-templates` | `manager`, `owner` | List checklist templates |
| `POST` | `/api/housekeeping/checklist-templates` | `manager`, `owner` | Create/update template |

#### 9.3.2 Maintenance

| Method | Endpoint | Permission | Description |
|--------|----------|-----------|-------------|
| `GET` | `/api/maintenance` | `maintenance`, `manager`, `owner` | List tickets (filters: status, severity, property) |
| `GET` | `/api/maintenance/{id}` | `maintenance`, `manager`, `owner` | Ticket detail |
| `POST` | `/api/maintenance` | `frontdesk`, `maintenance`, `manager`, `owner` | Create ticket |
| `PATCH` | `/api/maintenance/{id}/status` | `maintenance`, `manager` | Update status |
| `PATCH` | `/api/maintenance/{id}/assign` | `manager`, `owner` | Assign staff or vendor |
| `PATCH` | `/api/maintenance/{id}/cost` | `maintenance`, `manager` | Update cost estimate/actual |

#### 9.3.3 Inventory

| Method | Endpoint | Permission | Description |
|--------|----------|-----------|-------------|
| `GET` | `/api/inventory` | `frontdesk`, `manager`, `owner` | List inventory items (filter: property, category, low-stock) |
| `GET` | `/api/inventory/{id}` | `manager`, `owner` | Item detail with transaction history |
| `POST` | `/api/inventory` | `manager`, `owner` | Create item |
| `PUT` | `/api/inventory/{id}` | `manager`, `owner` | Update item (name, min/par levels) |
| `POST` | `/api/inventory/{id}/transactions` | `frontdesk`, `manager`, `owner` | Record stock in/out |
| `GET` | `/api/inventory/low-stock` | `manager`, `owner` | Items below minimum stock |
| `GET` | `/api/inventory/procurement-suggestions` | `manager`, `owner` | Procurement suggestions |
| `GET` | `/api/inventory/consumption-templates` | `manager`, `owner` | List consumption templates |
| `POST` | `/api/inventory/consumption-templates` | `manager`, `owner` | Create/update template |

#### 9.3.4 Vendors & Expenses

| Method | Endpoint | Permission | Description |
|--------|----------|-----------|-------------|
| `GET` | `/api/vendors` | `manager`, `owner` | List vendors |
| `POST` | `/api/vendors` | `owner` | Create vendor |
| `PUT` | `/api/vendors/{id}` | `owner` | Update vendor |
| `GET` | `/api/expenses` | `manager`, `owner` | List expenses (filter: property, category, date range, status) |
| `POST` | `/api/expenses` | `manager`, `owner` | Create expense entry |
| `PUT` | `/api/expenses/{id}` | `manager`, `owner` | Update expense |
| `PATCH` | `/api/expenses/{id}/payment` | `manager`, `owner` | Update payment status and amount |
| `GET` | `/api/reports/ops/monthly` | `manager`, `owner` | Monthly ops cost report |

#### 9.3.5 Incidents

| Method | Endpoint | Permission | Description |
|--------|----------|-----------|-------------|
| `GET` | `/api/incidents` | `frontdesk`, `manager`, `owner` | List incidents |
| `POST` | `/api/incidents` | `frontdesk`, `housekeeping`, `manager`, `owner` | Create incident |
| `PUT` | `/api/incidents/{id}` | `manager`, `owner` | Update incident |
| `PATCH` | `/api/incidents/{id}/status` | `manager`, `owner` | Update status |

---

## 10. Observability & Audit

### 10.1 Audit Trail

All operational entities use the existing `AuditLog` infrastructure:

| Entity | Audited Actions | Stored In |
|--------|----------------|-----------|
| `HousekeepingTask` | Status change, assignment, checklist update, damage flag, QC result | `AuditLog` with `EntityType = 'HousekeepingTask'` |
| `MaintenanceTicket` | Status change, assignment, cost update, vendor change | `AuditLog` with `EntityType = 'MaintenanceTicket'` |
| `InventoryItem` | Stock level change, min/par update | `AuditLog` with `EntityType = 'InventoryItem'` |
| `InventoryTransaction` | Created (append-only — never updated or deleted) | Self-auditing; no `AuditLog` entry needed |
| `Vendor` | Created, updated, deactivated | `AuditLog` with `EntityType = 'Vendor'` |
| `Expense` | Created, updated, payment status change | `AuditLog` with `EntityType = 'Expense'` |
| `Incident` | Status change, cost update, resolution | `AuditLog` with `EntityType = 'Incident'` |

> **OPS-AUD-001**: `AuditLog.PayloadJson` stores a before/after diff for updates. For status changes: `{"oldStatus": "X", "newStatus": "Y", "reason": "..."}`. Sensitive fields (bank info) MUST be redacted.

### 10.2 SLA Breach Events

When an SLA breach is detected (by `EscalationSweepJob` or inline), the system publishes an `OutboxMessage`:

| Event Type | Payload |
|------------|---------|
| `ops.housekeeping.sla_breach` | `{taskId, listingId, propertyId, cleaningType, dueAtUtc, currentStatus, escalationLevel}` |
| `ops.maintenance.sla_breach` | `{ticketId, listingId, propertyId, severity, slaType ('first_response' or 'resolution'), dueAtUtc}` |
| `ops.inventory.low_stock` | `{itemId, propertyId, itemName, currentStock, minimumStock}` |
| `ops.incident.created` | `{incidentId, listingId, propertyId, incidentType, severity}` |

### 10.3 Health Metrics

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| **Housekeeping SLA rate** | % of cleaning tasks completed before SLA deadline (rolling 7 days) | < 85% |
| **Maintenance first-response rate** | % of tickets with first response within SLA (rolling 7 days) | < 90% |
| **Maintenance resolution rate** | % of tickets resolved within SLA (rolling 7 days) | < 80% |
| **Unassigned task age** | Max age of any unassigned housekeeping task | > 1 hour |
| **Open P0 tickets** | Count of P0 tickets in non-terminal status | > 0 (immediate alert) |
| **Inventory items at zero** | Count of items with zero or negative stock | > 0 |

### 10.4 Debug Bundle for Support

On request, the system can export a debug bundle for a specific property covering the last 7 days:

| Content | Source |
|---------|--------|
| All housekeeping tasks | `HousekeepingTask` |
| All maintenance tickets | `MaintenanceTicket` |
| Inventory transactions | `InventoryTransaction` |
| SLA breach events | `OutboxMessage` filtered by ops event types |
| Audit log entries | `AuditLog` filtered by ops entity types |
| Escalation history | Escalation-related `AuditLog` entries |

> **OPS-AUD-002**: Debug bundle is JSON export. Available only to `owner` and Atlas Admin. Sensitive data (vendor bank info) is excluded.

---

## 11. Acceptance Criteria & Test Matrix

### 11.1 Given/When/Then Tests

#### HK-T01: Auto-create cleaning task on checkout

```
GIVEN a booking exists for Listing L1 with CheckoutDate = today
WHEN  the booking status is updated to CheckedOut (stay.checked_out event fires)
THEN  a HousekeepingTask is created with:
      - ListingId = L1
      - CleaningType = TURNOVER
      - Status = AUTO_CREATED or ASSIGNED (if staff auto-matched)
      - DueAtUtc = next booking's check-in time OR checkout + 4h (whichever is earlier)
      - BookingId = the completed booking
      - NextBookingId = the next upcoming booking (if any)
```

#### HK-T02: Prevent check-in if unit not "Ready"

```
GIVEN Listing L1 has room status = DIRTY (cleaning task exists but not COMPLETED)
WHEN  a user attempts to check in a guest (POST /api/bookings/{id}/checkin)
THEN  the API returns 409 Conflict with message "Unit not ready for check-in. Cleaning task pending."
AND   the check-in is blocked
```

#### HK-T03: Escalate overdue cleaning

```
GIVEN HousekeepingTask T1 has DueAtUtc = 14:00 UTC and Status = IN_PROGRESS
WHEN  the current time passes 14:00 UTC
THEN  EscalationSweepJob sets T1.EscalationLevel = 1
AND   an OutboxMessage with EventType = 'ops.housekeeping.sla_breach' is created
AND   the property manager is notified
```

#### HK-T04: Checklist completion tracking

```
GIVEN HousekeepingTask T1 has ChecklistTemplateId set with 5 items
WHEN  staff updates checklist progress to 3/5 items checked
THEN  ChecklistProgressJson is updated
AND   status remains IN_PROGRESS
WHEN  staff checks all 5 items and taps "Done"
THEN  status transitions to QC_PENDING (if QC enabled) or COMPLETED
```

#### MT-T01: Create maintenance ticket from booking

```
GIVEN a confirmed booking B1 for Listing L1
WHEN  front desk creates a maintenance ticket with:
      - TicketType = PLUMBING
      - Severity = HIGH
      - Description = "Bathroom tap leaking"
      - BookingId = B1
THEN  a MaintenanceTicket is created with Status = CREATED
AND   SLA timer starts (first response within 1 hour for HIGH)
AND   an OutboxMessage with EventType = 'ops.maintenance.created' is created
```

#### MT-T02: SLA escalation for P0 ticket

```
GIVEN MaintenanceTicket MT1 has Severity = P0 and Status = CREATED
WHEN  15 minutes pass without status change
THEN  EscalationSweepJob escalates the ticket
AND   tenant owner and Atlas Admin are notified via WhatsApp
AND   MT1.EscalationLevel is incremented
```

#### INV-T01: Inventory auto-deduct per check-in

```
GIVEN InventoryItem "Bath Towel" for Property P1 has CurrentStock = 20
AND   a consumption template exists: Bath Towel × 2 per check-in
WHEN  stay.checked_in event fires for a booking at Property P1
THEN  an InventoryTransaction is created with TransactionType = CHECKIN_DEDUCT, Quantity = -2
AND   InventoryItem.CurrentStock is updated to 18
```

#### INV-T02: Low stock alert triggers

```
GIVEN InventoryItem "Soap Bar" for Property P1 has MinimumStock = 10 and CurrentStock = 12
WHEN  a CHECKIN_DEDUCT of 3 brings CurrentStock to 9
THEN  CurrentStock (9) < MinimumStock (10)
AND   an OutboxMessage with EventType = 'ops.inventory.low_stock' is created
AND   property manager is notified
```

#### EXP-T01: Expense report totals match entries

```
GIVEN 5 expense entries exist for Property P1 in January 2026:
      - LINEN: ₹5,000, ₹3,000
      - CLEANING: ₹2,000
      - MAINTENANCE: ₹8,000
      - SALARY: ₹15,000
WHEN  the monthly ops cost report is generated for P1, January 2026
THEN  Total = ₹33,000
AND   Category breakdown: LINEN ₹8,000, CLEANING ₹2,000, MAINTENANCE ₹8,000, SALARY ₹15,000
AND   per-night cost = ₹33,000 / occupied nights
```

#### VND-T01: Vendor payment status update

```
GIVEN Expense E1 has Amount = ₹10,000, PaymentStatus = UNPAID, AmountPaid = 0
WHEN  manager updates PaymentStatus to PARTIAL with AmountPaid = ₹6,000
THEN  E1.PaymentStatus = PARTIAL, AmountPaid = ₹6,000
AND   AuditLog records the change with old/new values
WHEN  manager updates AmountPaid to ₹10,000
THEN  E1.PaymentStatus = PAID, AmountPaid = ₹10,000
```

#### INC-T01: Damage flag creates linked incident

```
GIVEN HousekeepingTask T1 for Listing L1 is IN_PROGRESS
WHEN  staff flags damage with description "Cracked bathroom mirror"
THEN  an Incident is created with:
      - IncidentType = DAMAGE
      - ListingId = L1
      - LinkedHousekeepingTaskId = T1.Id
      - Status = OPEN
AND   T1.DamageFlag = true, T1.LinkedIncidentId = Incident.Id
AND   property manager is notified
```

### 11.2 Edge Case Test Matrix

| ID | Scenario | Expected Behavior |
|----|----------|-------------------|
| EC-01 | Checkout with no next booking | DueAtUtc = checkout + default SLA (4h); no NextBookingId |
| EC-02 | Back-to-back bookings (checkout and check-in same day) | Turnover SLA = next check-in time; high-priority task |
| EC-03 | Booking cancelled after cleaning task created | Task status → `CANCELLED` (if still `AUTO_CREATED` or `ASSIGNED`); leave as-is if already `IN_PROGRESS` |
| EC-04 | Staff assigned to task is deactivated | Task reassigned via workload distribution; if no staff available, alert manager |
| EC-05 | Multiple P0 maintenance tickets for same property | Each gets its own SLA timer; all managers notified for each |
| EC-06 | Inventory stock goes negative | Allowed (deduction still occurs); zero-stock alert fires |
| EC-07 | Expense created with no vendor | Valid; `VendorId` is nullable |
| EC-08 | Two checkout events for same booking (duplicate event) | Idempotency: check if `HousekeepingTask` already exists for `(BookingId, CleaningType=TURNOVER)`; skip if exists |
| EC-09 | Tenant with no staff configured | All tasks remain `AUTO_CREATED` (unassigned); owner alerted |
| EC-10 | Property with no consumption template | No auto-deduct on check-in; task proceeds without inventory impact |
| EC-11 | Concurrent check-out for multiple rooms at same property | Each creates independent task; workload distribution handles round-robin across all |
| EC-12 | Mid-stay cleaning scheduled but guest checks out early | Scheduled `AutomationSchedule` entries with `DueAtUtc` after checkout are cancelled |

---

## 12. Rollout Plan

### 12.1 Feature Flags

| Flag | Default | Controls |
|------|---------|----------|
| `ops.housekeeping.enabled` | `false` | Entire housekeeping module |
| `ops.housekeeping.auto_create_on_checkout` | `true` | Auto-create turnover task on checkout |
| `ops.housekeeping.qc_enabled` | `false` | Whether QC_PENDING step is required |
| `ops.housekeeping.checklist_enabled` | `true` | Whether checklist templates are used |
| `ops.maintenance.enabled` | `false` | Entire maintenance module |
| `ops.maintenance.auto_close_after_48h` | `true` | Auto-close verified tickets |
| `ops.inventory.enabled` | `false` | Entire inventory module |
| `ops.inventory.auto_deduct_on_checkin` | `true` | Auto-deduct consumables on check-in |
| `ops.vendors.enabled` | `false` | Vendor directory and expenses |
| `ops.incidents.enhanced` | `false` | Enhanced incident model (vs. legacy) |
| `ops.staff.workload_distribution` | `true` | Auto-assignment of tasks |

> **OPS-ROLL-001**: Feature flags are stored in `TenantFeatureFlags` table (if exists) or `appsettings.json`. Per-tenant override supported. Platform-level defaults in appsettings.

### 12.2 Migration & Backfill

| Migration | Description | Risk |
|-----------|-------------|------|
| **Incident table migration** | Add new columns (`TenantId`, `PropertyId`, `IncidentType`, `Severity`, etc.) with defaults. Backfill `TenantId` from `Listing.TenantId` via `ListingId`. Backfill `PropertyId` from `Listing.PropertyId` | Low — additive columns; existing data preserved |
| **New table creation** | `HousekeepingTask`, `HousekeepingChecklistTemplate`, `MaintenanceTicket`, `InventoryItem`, `InventoryTransaction`, `Vendor`, `Expense`, `InventoryConsumptionTemplate` | Low — new tables, no existing data affected |
| **User table extension** | Add `AssignedPropertyIds`, `IsFieldStaff`, `MaxDailyTasks` | Low — nullable columns |

> **OPS-ROLL-002**: All migrations MUST be additive (new columns nullable or with defaults, new tables). No breaking changes to existing schema.

### 12.3 Beta Rollout Phases

| Phase | Duration | Scope | Gate |
|-------|----------|-------|------|
| **Phase 0: Internal** | 2 weeks | Atlas-managed properties only (1–2 properties) | All CRUD works, today board renders, automation fires |
| **Phase 1: Housekeeping** | 2 weeks | 3–5 beta tenants | Housekeeping module + basic maintenance |
| **Phase 2: Full Ops** | 2 weeks | Same beta tenants | Inventory, vendors, expenses, enhanced incidents |
| **Phase 3: GA** | Ongoing | All tenants (feature flag gated) | Reports validated, SLA compliance > 85%, no critical bugs |

### 12.4 Success Metrics

| Metric | Target | Measurement |
|--------|--------|------------|
| **Time saved per turnover** | 15 min saved per turnover (vs. WhatsApp coordination) | Staff survey + task timestamp analysis |
| **Missed cleaning incidents** | Reduce by 80% (vs. manual tracking) | Incident count before/after |
| **Guest complaints about cleanliness** | Reduce by 50% | Booking review analysis |
| **Maintenance response time** | < 2 hours for HIGH, < 30 min for P0 | SLA compliance reports |
| **Inventory stockout events** | < 2 per property per month | Low-stock alert frequency |
| **Staff adoption** | > 80% of assigned tasks completed via app (vs. WhatsApp/phone) | Task completion analytics |
| **Ops cost visibility** | 100% of properties have monthly cost report | Report generation logs |

---

## 13. Definition of Done — Ops Core V1

### 13.1 Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | Housekeeping task auto-created on checkout event | ☐ |
| 2 | Today Board (Kanban) renders correctly on desktop and mobile | ☐ |
| 3 | Room Status Grid shows accurate computed status for all listings | ☐ |
| 4 | Staff assignment (round-robin) works for housekeeping tasks | ☐ |
| 5 | All housekeeping status transitions validated and audited | ☐ |
| 6 | Checklist templates CRUD and runtime progress tracking works | ☐ |
| 7 | Damage flag creates linked Incident | ☐ |
| 8 | QC workflow (approve/reject) works when enabled | ☐ |
| 9 | Maintenance ticket CRUD with full lifecycle | ☐ |
| 10 | Maintenance severity-based SLA timers and escalation working | ☐ |
| 11 | Maintenance ticket linked to booking/vendor/expense | ☐ |
| 12 | Cost tracking (estimate vs actual) on maintenance tickets | ☐ |
| 13 | Inventory items CRUD with stock in/out transactions | ☐ |
| 14 | Auto-deduct on check-in via consumption templates | ☐ |
| 15 | Low-stock and zero-stock alerts fire correctly | ☐ |
| 16 | Procurement suggestions generated for below-minimum items | ☐ |
| 17 | Vendor directory CRUD | ☐ |
| 18 | Expense entries CRUD with payment status lifecycle | ☐ |
| 19 | Monthly ops cost report generates accurately | ☐ |
| 20 | Enhanced Incident model with type, severity, cost, and linking | ☐ |
| 21 | Staff roles and permissions enforced on all ops endpoints | ☐ |
| 22 | All tenant-view reports (§8.1) accessible and data-accurate | ☐ |
| 23 | Check-in blocked if room status ≠ READY (409 response) | ☐ |
| 24 | SLA breach escalation works via EscalationSweepJob | ☐ |

### 13.2 Non-Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | All entities implement `ITenantOwnedEntity` with global query filter | ☐ |
| 2 | No cross-tenant data leakage (verified by integration tests) | ☐ |
| 3 | All status transitions recorded in `AuditLog` | ☐ |
| 4 | Idempotency: duplicate checkout event does not create duplicate task | ☐ |
| 5 | Mobile-friendly UI: all ops screens usable on 360px-wide screen | ☐ |
| 6 | Today Board loads in < 500ms for 50 tasks | ☐ |
| 7 | Room status endpoint responds in < 200ms | ☐ |
| 8 | Monthly report query completes in < 2 seconds for 1 year of data | ☐ |
| 9 | Stock balance reconciliation job runs nightly and logs discrepancies | ☐ |
| 10 | Vendor bank info encrypted at rest | ☐ |
| 11 | Feature flags control module activation per tenant | ☐ |
| 12 | EF Core migrations are additive (no data loss) | ☐ |
| 13 | All acceptance tests (§11) pass | ☐ |
| 14 | Debug bundle export works for support scenarios | ☐ |

---

## Appendix A — Event Types (Ops Domain)

New event types to add to `EventTypes.cs`:

| Constant | Value | Trigger |
|----------|-------|---------|
| `OpsHousekeepingCreated` | `ops.housekeeping.created` | Housekeeping task auto-created or manually created |
| `OpsHousekeepingAssigned` | `ops.housekeeping.assigned` | Task assigned to staff |
| `OpsHousekeepingCompleted` | `ops.housekeeping.completed` | Task completed (room now ready) |
| `OpsHousekeepingSlaBreach` | `ops.housekeeping.sla_breach` | Task overdue |
| `OpsHousekeepingUnassigned` | `ops.housekeeping.unassigned` | No staff available for auto-assignment |
| `OpsMaintenanceCreated` | `ops.maintenance.created` | Maintenance ticket created |
| `OpsMaintenanceSlaBreach` | `ops.maintenance.sla_breach` | Ticket SLA breached |
| `OpsMaintenanceResolved` | `ops.maintenance.resolved` | Ticket resolved |
| `OpsInventoryLowStock` | `ops.inventory.low_stock` | Stock below minimum |
| `OpsInventoryZeroStock` | `ops.inventory.zero_stock` | Stock at or below zero |
| `OpsIncidentCreated` | `ops.incident.created` | Incident reported |

Helper method to add:

```csharp
public static bool IsOpsEvent(string eventType) =>
    eventType.StartsWith("ops.", StringComparison.Ordinal);
```

---

## Appendix B — Cross-Reference to Existing Models

| Existing Entity | Relationship to Ops Module |
|----------------|---------------------------|
| `Booking` | Triggers housekeeping tasks (checkout), links to maintenance tickets, incidents, inventory deductions |
| `Listing` | Every ops entity links to a listing (room/unit); room status derived from listing's tasks |
| `Property` | Ops entities denormalize `PropertyId` for dashboard queries; inventory tracked per property |
| `User` | Staff profiles; assigned to tasks and tickets; extended with ops fields |
| `Guest` | Linked to incidents (guest-reported issues); linked to bookings that trigger ops workflows |
| `AuditLog` | All ops status changes logged here |
| `AutomationSchedule` | Mid-stay cleaning schedules; future preventive maintenance schedules |
| `AutomationRule` (RA-AUTO-001) | Rules for auto-creating housekeeping tasks, escalation triggers |
| `OutboxMessage` | All ops events published via outbox for at-least-once delivery |
| `CommunicationLog` | Tracks WhatsApp/SMS notifications sent for SLA breaches, alerts |
| `Incident` (existing) | Migrated and enhanced with new fields; becomes full ops entity |
| `StaffTask` (RA-AUTO-001) | `HousekeepingTask` and `MaintenanceTicket` are specialized, richer replacements. `StaffTask` remains for generic automation-created tasks not covered by ops modules |

---

*End of RA-OPS-001*
