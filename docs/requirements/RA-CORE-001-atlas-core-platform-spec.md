# RA-CORE-001 — Atlas Core Multi-Tenant Platform Specification

| Field | Value |
|-------|-------|
| **Doc ID** | RA-CORE-001 |
| **Title** | Atlas Core — Multi-Tenant Platform Specification |
| **Status** | DRAFT |
| **Author** | Atlas Architecture |
| **Created** | 2026-02-27 |
| **Depends on** | — (foundational; all other RAs depend on this) |
| **Consumers** | All Atlas vertical products (PMS, Legal, CA, Clinic, …) |

---

## Table of Contents

1. [Vision & Principles](#1-vision--principles)
2. [Tenant Lifecycle Management](#2-tenant-lifecycle-management)
3. [Identity & Access Management (IAM)](#3-identity--access-management-iam)
4. [Subscription & Billing Framework](#4-subscription--billing-framework)
5. [Atlas Connect (Integration Framework)](#5-atlas-connect-integration-framework)
6. [Atlas Automations (Platform Engine)](#6-atlas-automations-platform-engine)
7. [Atlas Notifications (Communication Engine)](#7-atlas-notifications-communication-engine)
8. [Atlas Sites (Multi-Tenant Public Site Framework)](#8-atlas-sites-multi-tenant-public-site-framework)
9. [Atlas Insights (Analytics Foundation)](#9-atlas-insights-analytics-foundation)
10. [Audit & Compliance Layer](#10-audit--compliance-layer)
11. [Multi-Vertical Extensibility Model](#11-multi-vertical-extensibility-model)
12. [Database Schema Separation Strategy](#12-database-schema-separation-strategy)
13. [Performance & Scalability](#13-performance--scalability)
14. [Observability & Platform Health](#14-observability--platform-health)
15. [Security Model](#15-security-model)
16. [Definition of Done — Atlas Core V1](#16-definition-of-done--atlas-core-v1)

**Appendix A** — [Existing Entity Mapping](#appendix-a--existing-entity-mapping)
**Appendix B** — [Vertical Registration Example](#appendix-b--vertical-registration-example)

---

## 1. Vision & Principles

### 1.1 What Atlas Core IS

Atlas Core is the **vertical-agnostic platform layer** that powers every Atlas product. It owns:

| Capability | Core Responsibility |
|-----------|-------------------|
| **Tenancy** | Tenant creation, suspension, configuration, isolation |
| **Identity** | Users, roles, permissions, authentication, authorization |
| **Billing** | Plans, subscriptions, invoices, payments, credits |
| **Integrations** | OAuth connectors, API-key connectors, token lifecycle |
| **Automations** | Event-driven and time-driven automation engine |
| **Notifications** | Template-based, multi-channel messaging |
| **Sites** | Tenant-branded public pages with slug/custom-domain routing |
| **Insights** | Event storage, snapshot scheduling, tenant reporting base |
| **Audit** | Immutable change log, compliance export |
| **Security** | Encryption, isolation, rate limiting, authorization enforcement |

### 1.2 What Atlas Core is NOT

| NOT | Explanation |
|-----|-------------|
| A hospitality system | Core knows nothing about bookings, listings, properties, guests, or housekeeping |
| A microservices platform | Modular monolith — one deployable unit, logical module boundaries |
| An event bus | DB-backed outbox only; no RabbitMQ, Kafka, or Service Bus dependency |
| A CRM | No built-in CRM logic; verticals add their own customer models |
| An ERP | No accounting, HR, or supply-chain logic |
| A code-generation framework | Verticals are hand-written modules, not generated from metadata |

### 1.3 Separation of Concerns

```
┌─────────────────────────────────────────────────────┐
│                   Atlas API (Modular Monolith)        │
│  ┌────────────┐  ┌────────────┐  ┌────────────┐     │
│  │  Atlas PMS  │  │ Atlas Legal │  │ Atlas CA   │ ... │
│  │  (vertical) │  │ (vertical)  │  │ (vertical) │     │
│  └──────┬─────┘  └──────┬─────┘  └──────┬─────┘     │
│         │               │               │            │
│  ═══════╪═══════════════╪═══════════════╪════════    │
│         │               │               │            │
│  ┌──────┴───────────────┴───────────────┴─────┐      │
│  │                 ATLAS CORE                  │      │
│  │  Tenancy · IAM · Billing · Connect ·       │      │
│  │  Automations · Notifications · Sites ·     │      │
│  │  Insights · Audit · Security               │      │
│  └────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────┘
```

**Boundary rules:**

| Rule | Enforcement |
|------|-------------|
| Core MUST NOT reference any vertical namespace (`Atlas.Api.Pms`, `Atlas.Api.Legal`, etc.) | Compile-time: namespace dependency analysis; CI check |
| Verticals MAY depend on Core interfaces and entities | Normal project reference |
| Verticals MUST NOT depend on other verticals | Compile-time: namespace dependency analysis |
| Shared abstractions live in Core | Interfaces, base classes, extension points |
| Vertical-specific configuration extends Core's `TenantConfig` model | JSON-typed extension column |

### 1.4 Architectural Principles

| # | Principle | Implication |
|---|-----------|-------------|
| 1 | **Tenant isolation** | Every row belongs to a tenant. Global EF Core query filter on `TenantId`. No cross-tenant reads except platform admin |
| 2 | **Config-driven behavior** | Feature toggles, plan limits, SLA thresholds, and notification rules are configuration — not code branches |
| 3 | **Extensibility-first** | Every Core capability exposes registration points for verticals: event types, action types, role templates, menu items, plan features |
| 4 | **Lean V1** | Ship the 80% that covers 80% of verticals. Do not build a generic workflow engine, a form builder, or a report designer |
| 5 | **No overengineering** | One database. One deployable. One codebase. No distributed transactions. No saga orchestrator |
| 6 | **Platform-first mindset** | When adding a feature, ask: "Is this vertical-specific or would any vertical need this?" If the latter, build it in Core |
| 7 | **Single developer maintainability** | Every abstraction must be understandable in < 5 minutes. Prefer explicit code over clever frameworks |
| 8 | **Deterministic and auditable** | Every state change is traceable. No fire-and-forget. No silent failures |

---

## 2. Tenant Lifecycle Management

### 2.1 Tenant Model (Existing)

The existing `Tenant` entity is the foundation:

| Field | Type | Current | Core Mapping |
|-------|------|---------|-------------|
| `Id` | `int` PK | ✓ | Tenant identity |
| `Name` | `nvarchar(100)` | ✓ | Display name |
| `Slug` | `varchar(50)` | ✓ | URL slug for public site |
| `IsActive` | `bit` | ✓ | Active/suspended |
| `OwnerName` | `nvarchar(100)` | ✓ | Primary contact |
| `OwnerEmail` | `nvarchar(200)` | ✓ | Primary email |
| `OwnerPhone` | `varchar(20)` | ✓ | Primary phone |
| `CustomDomain` | `nvarchar(500)` | ✓ | Custom domain |
| `LogoUrl` | `nvarchar(500)` | ✓ | Brand logo |
| `BrandColor` | `varchar(7)` | ✓ | Brand hex color |
| `Plan` | `varchar(20)` | ✓ | Legacy plan field (superseded by `TenantSubscription`) |
| `CreatedAtUtc` | `datetime2` | ✓ | |

**New Core fields:**

| Field | Type | Description |
|-------|------|-------------|
| `VerticalCode` | `varchar(10)` | `PMS`, `LEGAL`, `CA`, `CLINIC`, etc. Determines which product module is active |
| `Status` | `varchar(20)` | `PENDING_VERIFICATION`, `ACTIVE`, `SUSPENDED`, `DELETED` (replaces simple `IsActive` bit) |
| `SuspendedAtUtc` | `datetime2?` | When suspension started |
| `SuspensionReason` | `varchar(30)` | `BILLING`, `ABUSE`, `MANUAL`, `COMPLIANCE` |
| `DeletedAtUtc` | `datetime2?` | Soft-delete timestamp |
| `ConfigJson` | `nvarchar(max)` | Tenant-level configuration (feature flags, thresholds, preferences) |
| `Timezone` | `varchar(50)` | IANA timezone (e.g., `Asia/Kolkata`) |
| `Country` | `varchar(2)` | ISO 3166 country code |
| `UpdatedAtUtc` | `datetime2` | |

> **CORE-TN-001**: The `IsActive` field is retained for backward compatibility but derived from `Status = 'ACTIVE'`. New code MUST use `Status`.

### 2.2 Tenant Lifecycle State Machine

```
         ┌───── PENDING_VERIFICATION ─────┐
         │                                 │
   (owner completes verification)          │ (expires after 30 days)
         │                                 │
         ▼                                 ▼
      ACTIVE ◄──── (reactivation) ──── SUSPENDED ───► DELETED
         │                                 ▲              (soft)
         │                                 │
         └── (billing failure, abuse) ─────┘
```

| Transition | Trigger | System Action |
|-----------|---------|---------------|
| `→ PENDING_VERIFICATION` | Tenant created (signup) | Create `TenantSubscription` with `Trial` status; send verification email |
| `PENDING → ACTIVE` | Owner verifies email + completes profile (`TenantProfile.OnboardingStatus = 'Complete'`) | Enable all plan features; start trial clock |
| `ACTIVE → SUSPENDED` | Billing failure (invoice overdue + grace period elapsed) OR manual admin action OR abuse detection | Set `SuspendedAtUtc`; block write operations; read-only access preserved |
| `SUSPENDED → ACTIVE` | Payment received OR admin reactivation | Clear `SuspendedAtUtc`; restore write access |
| `SUSPENDED → DELETED` | Admin action after 90 days suspended OR tenant self-service deletion request | Set `DeletedAtUtc`; anonymize PII after 30 days; retain financial records |
| `ACTIVE → DELETED` | Tenant self-service deletion | Via SUSPENDED first (immediate suspension, then deletion after confirmation) |

> **CORE-TN-002**: `DELETED` is soft-delete. Data is retained for 90 days (audit/financial compliance), then PII is anonymized. Financial records (invoices, payments) are retained for 7 years per Indian tax law.

### 2.3 Tenant Configuration Storage

`Tenant.ConfigJson` stores a JSON document of tenant-level settings:

```json
{
  "features": {
    "ops.housekeeping.enabled": true,
    "ops.inventory.enabled": false,
    "retention.campaigns.enabled": true
  },
  "limits": {
    "maxUsers": 10,
    "maxListings": 50,
    "maxCampaignsPerMonth": 5
  },
  "preferences": {
    "quietHoursStart": "21:00",
    "quietHoursEnd": "08:00",
    "defaultCurrency": "INR",
    "dateFormat": "DD/MM/YYYY"
  }
}
```

| Section | Purpose | Override Source |
|---------|---------|---------------|
| `features` | Feature flags (boolean toggles) | Merged: plan defaults → admin override → tenant self-service (allowed set only) |
| `limits` | Usage caps tied to plan | Set by plan; admin can override upward |
| `preferences` | Tenant-adjustable settings | Tenant self-service |

> **CORE-TN-003**: Feature flag evaluation: `PlanFeature ∪ AdminOverride ∪ TenantConfig`. Plan sets defaults; admin can force-enable/disable; tenant can toggle self-service flags.

### 2.4 Subscription State Machine

See §4 for full billing lifecycle. Summary states:

```
TRIAL → ACTIVE → PAST_DUE → SUSPENDED → CANCELED
                     ↑          │
                     └──────────┘ (payment received)
```

---

## 3. Identity & Access Management (IAM)

### 3.1 Entities

#### 3.1.1 `User` (Existing — Extended)

Current: `Id, TenantId, Name, Phone, Email, PasswordHash, Role`

**New Core fields:**

| Field | Type | Description |
|-------|------|-------------|
| `Status` | `varchar(15)` | `ACTIVE`, `INVITED`, `DISABLED` |
| `LastLoginAtUtc` | `datetime2?` | Last successful authentication |
| `InvitedByUserId` | `int?` | Who invited this user |
| `InvitedAtUtc` | `datetime2?` | When invitation was sent |
| `MustResetPassword` | `bit` | Force password reset on next login |
| `RefreshTokenHash` | `varchar(200)` | Hashed refresh token for JWT rotation |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

> **CORE-IAM-001**: The existing `Role` string field is retained for backward compatibility. New RBAC uses `RoleAssignment` (many-to-many). The `Role` field is kept in sync as the **primary role** for simple lookups.

#### 3.1.2 `Role`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `Code` | `varchar(30)` | Unique code (e.g., `pms.owner`, `pms.housekeeping`, `legal.partner`) |
| `Name` | `nvarchar(100)` | Human-readable name |
| `VerticalCode` | `varchar(10)` | Which vertical this role belongs to. `CORE` for platform roles |
| `IsSystem` | `bit` | Platform-defined (cannot be deleted by tenant) |
| `Description` | `nvarchar(300)` | |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_Role_Code`.

**Seed data (Core roles):**

| Code | Name | Vertical |
|------|------|----------|
| `core.superadmin` | Atlas Super Admin | `CORE` |
| `core.admin` | Atlas Admin | `CORE` |
| `core.support` | Atlas Support | `CORE` |

**PMS roles (seeded by PMS module):**

| Code | Name |
|------|------|
| `pms.owner` | Property Owner |
| `pms.manager` | Property Manager |
| `pms.frontdesk` | Front Desk |
| `pms.housekeeping` | Housekeeping |
| `pms.maintenance` | Maintenance |

#### 3.1.3 `Permission`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `Code` | `varchar(80)` | e.g., `bookings.create`, `expenses.view`, `tenants.manage` |
| `Name` | `nvarchar(150)` | |
| `Module` | `varchar(20)` | `core`, `pms`, `legal`, `ca`, etc. |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_Permission_Code`.

#### 3.1.4 `RolePermission`

| Field | Type | Description |
|-------|------|-------------|
| `RoleId` | `int` (FK → Role) | |
| `PermissionId` | `int` (FK → Permission) | |

**Composite PK**: `(RoleId, PermissionId)`.

#### 3.1.5 `RoleAssignment`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `UserId` | `int` (FK → User) | |
| `RoleId` | `int` (FK → Role) | |
| `TenantId` | `int` (FK → Tenant) | |
| `AssignedByUserId` | `int?` | |
| `AssignedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_RoleAssignment_UserId_RoleId_TenantId`.

> **CORE-IAM-002**: A user can have multiple roles within a tenant (e.g., `pms.manager` + `pms.frontdesk`). Effective permissions = union of all assigned role permissions.

### 3.2 JWT Claims Model

| Claim | Source | Example |
|-------|--------|---------|
| `sub` | `User.Id` | `123` |
| `tid` | `User.TenantId` | `7` |
| `name` | `User.Name` | `"Priya Sharma"` |
| `email` | `User.Email` | `"priya@example.com"` |
| `roles` | `RoleAssignment` → `Role.Code` (array) | `["pms.owner", "pms.manager"]` |
| `vertical` | `Tenant.VerticalCode` | `"PMS"` |
| `iat`, `exp` | Standard JWT | |

**Token lifetimes:**

| Token | Lifetime | Storage |
|-------|----------|---------|
| Access token (JWT) | 15 minutes | Client memory (never localStorage) |
| Refresh token | 7 days | HttpOnly secure cookie + hashed in `User.RefreshTokenHash` |

### 3.3 Route-Level Authorization

```
[Authorize(Roles = "pms.owner,pms.manager")]       // Role-based
[RequirePermission("bookings.create")]              // Permission-based (custom attribute)
[RequireTenantActive]                               // Tenant status check
[RequireSubscriptionActive]                         // Subscription status check
```

**Enforcement pipeline:**

```
Request → JWT validation → TenantId extraction → Tenant status check
  → Subscription check → Role/Permission check → Controller action
```

> **CORE-IAM-003**: `RequirePermission` is a custom authorization attribute that resolves the user's effective permissions from `RoleAssignment` → `RolePermission` → `Permission`. Results are cached per user session (in-memory, 5-minute TTL).

### 3.4 Cross-Tenant Isolation

| Rule | Enforcement |
|------|-------------|
| Users can only access data within their `TenantId` | Global EF Core query filter on `ITenantOwnedEntity` |
| Users cannot impersonate another tenant | `TenantId` comes from JWT `tid` claim, not request body |
| Super-admin can access any tenant | Super-admin endpoints bypass tenant filter via `.IgnoreQueryFilters()` — audited |

### 3.5 Super-Admin Override

| Capability | Access | Audit |
|-----------|--------|-------|
| View any tenant's data | `core.superadmin` or `core.admin` | Every cross-tenant access logged with `AuditLog.Action = 'admin.cross_tenant_access'` |
| Impersonate tenant | `core.superadmin` only; sets `tid` in request context | Full session logged |
| Suspend/delete tenant | `core.superadmin` only | AuditLog + OutboxMessage notification to tenant owner |

---

## 4. Subscription & Billing Framework

### 4.1 Existing Billing Entities

The platform already has a mature billing model:

| Entity | Purpose | Core Status |
|--------|---------|-------------|
| `BillingPlan` | Plan definitions (Code, Name, MonthlyPriceInr, CreditsIncluded, SeatLimit, ListingLimit) | ✓ Exists — extend |
| `TenantSubscription` | Per-tenant subscription state (Trial, Active, PastDue, Suspended, Canceled) | ✓ Exists — extend |
| `BillingInvoice` | Invoice with period, amounts, GST, status | ✓ Exists |
| `BillingPayment` | Payment attempts linked to invoices | ✓ Exists |
| `TenantCreditsLedger` | Append-only credits ledger (Grant, Debit, Adjust, Expire) | ✓ Exists |

### 4.2 Plan Definition Model (Extended)

**New fields on `BillingPlan`:**

| Field | Type | Description |
|-------|------|-------------|
| `VerticalCode` | `varchar(10)` | Which vertical this plan applies to (`PMS`, `LEGAL`, `ALL`) |
| `FeaturesJson` | `nvarchar(max)` | JSON map of feature flags enabled by this plan |
| `LimitsJson` | `nvarchar(max)` | JSON map of usage limits (seats, entities, storage) |
| `TrialDays` | `int` | Trial period length (0 = no trial) |
| `GracePeriodDays` | `int` | Days after invoice due before suspension |
| `IsPublic` | `bit` | Visible on pricing page (vs. enterprise/custom plans) |
| `SortOrder` | `int` | Display order on pricing page |
| `AnnualPriceInr` | `decimal(18,2)?` | Annual price (discount vs. 12× monthly) |
| `Description` | `nvarchar(500)` | Plan description for pricing page |

**Example `FeaturesJson`:**
```json
{
  "ops.housekeeping.enabled": true,
  "ops.inventory.enabled": true,
  "retention.campaigns.enabled": false,
  "connect.channex.enabled": true,
  "insights.nlq.enabled": false
}
```

**Example `LimitsJson`:**
```json
{
  "maxUsers": 5,
  "maxListings": 10,
  "maxProperties": 3,
  "maxCampaignsPerMonth": 0,
  "maxStorageMb": 500
}
```

> **CORE-BIL-001**: `FeaturesJson` and `LimitsJson` are the source of truth for plan capabilities. `Tenant.ConfigJson.features` overrides are merged on top of plan defaults at evaluation time.

### 4.3 Subscription State Machine

```
TRIAL ──────────────────────────────────► ACTIVE
  │ (trial expires, no payment)              │
  ▼                                          │ (invoice unpaid after due date)
SUSPENDED ◄───── PAST_DUE ◄─────────────────┘
  │                  │
  │ (payment)        │ (payment)
  │                  ▼
  │              ACTIVE
  │
  │ (90 days suspended, no action)
  ▼
CANCELED
```

| Status | User Experience | System Behavior |
|--------|----------------|-----------------|
| `Trial` | Full access to plan features | Trial countdown shown; CTA to subscribe |
| `Active` | Full access | Normal operation |
| `PastDue` | Full access (grace period) | Warning banner; daily payment reminder; `GracePeriodDays` countdown |
| `Suspended` | Read-only access; no create/update | Blocked at authorization middleware; prominent reactivation CTA |
| `Canceled` | No access; redirect to reactivation page | All features blocked; data preserved for 90 days |

### 4.4 Plan Upgrade / Downgrade

| Flow | Timing | Billing |
|------|--------|---------|
| **Upgrade** | Immediate — new features available now | Prorated charge for remainder of current period |
| **Downgrade** | End of current period — no feature loss until renewal | Next invoice at lower plan rate |
| **Cancel** | End of current period → `Canceled` | No further invoices |

> **CORE-BIL-002**: Plan changes are logged in `AuditLog` and `TenantCreditsLedger` (if credits differ). Downgrade checks usage against new plan limits — if current usage exceeds new limits, downgrade is blocked with a clear error.

### 4.5 Enforcement Strategy

| Type | Behavior | When |
|------|----------|------|
| **Soft block** | Warning banner + email to owner | Invoice overdue, usage at 80% of limit |
| **Hard block** | Write operations blocked; read-only mode | Subscription `Suspended`; usage exceeds 100% of limit |
| **Feature block** | Specific feature disabled | Feature not included in plan |

**Enforcement middleware:**

```
Request → [SubscriptionEnforcementMiddleware]
  → Check TenantSubscription.Status
  → If Suspended/Canceled → reject writes (403)
  → Check UsageLimits vs current usage
  → If exceeded → reject entity creation (403)
  → Check FeatureFlags
  → If feature disabled → reject (403)
  → Pass to controller
```

### 4.6 Payment Integration (Razorpay)

| Feature | V1 | V2 |
|---------|----|----|
| Payment gateway | Razorpay | Multi-gateway (Stripe for international) |
| Invoice generation | System-generated; Razorpay payment link | Auto-collect via subscription |
| Payment webhook | `/api/webhooks/razorpay/payment` → updates `BillingPayment` → `BillingInvoice` | Same |
| Refunds | Manual via Razorpay dashboard | API-driven refund flow |
| UPI autopay | V2 | Razorpay subscription with UPI mandate |

---

## 5. Atlas Connect (Integration Framework)

### 5.1 Design Goal

A vertical-agnostic framework for connecting tenant accounts to third-party services. PMS uses it for Channex and Razorpay. Legal might use it for courts API. CA might use it for GST portal.

### 5.2 Entities

#### 5.2.1 `IntegrationProvider`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `Code` | `varchar(30)` | e.g., `channex`, `razorpay`, `gst_portal`, `ecourt` |
| `Name` | `nvarchar(100)` | Display name |
| `VerticalCode` | `varchar(10)` | Which vertical uses this provider. `ALL` for cross-vertical |
| `AuthType` | `varchar(15)` | `OAUTH2`, `API_KEY`, `BASIC`, `NONE` |
| `BaseUrl` | `nvarchar(500)` | Provider API base URL |
| `ConfigSchemaJson` | `nvarchar(max)` | JSON Schema for provider-specific configuration |
| `IsActive` | `bit` | |
| `CreatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_IntegrationProvider_Code`.

#### 5.2.2 `TenantIntegration`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `TenantId` | `int` | |
| `ProviderId` | `int` (FK → IntegrationProvider) | |
| `Status` | `varchar(15)` | `ACTIVE`, `DISABLED`, `ERROR`, `PENDING_AUTH` |
| `ConfigJson` | `nvarchar(max)` | Provider-specific tenant configuration (encrypted sensitive values) |
| `LastHealthCheckUtc` | `datetime2?` | |
| `LastHealthStatus` | `varchar(15)` | `HEALTHY`, `DEGRADED`, `DOWN` |
| `EnabledByUserId` | `int` | |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

**Unique constraint**: `IX_TenantIntegration_TenantId_ProviderId`.

#### 5.2.3 `IntegrationToken`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `int` PK | |
| `TenantIntegrationId` | `int` (FK) | |
| `TokenType` | `varchar(15)` | `ACCESS`, `REFRESH`, `API_KEY` |
| `EncryptedValue` | `nvarchar(max)` | AES-256-GCM encrypted token value |
| `ExpiresAtUtc` | `datetime2?` | NULL for non-expiring API keys |
| `LastUsedAtUtc` | `datetime2?` | |
| `CreatedAtUtc` | `datetime2` | |
| `UpdatedAtUtc` | `datetime2` | |

> **CORE-INT-001**: Token values are NEVER stored in plaintext. Encryption key is per-environment, stored in Azure Key Vault (or `appsettings` for dev). Decryption happens only at the moment of API call.

#### 5.2.4 `IntegrationHealthLog`

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `long` PK | |
| `TenantIntegrationId` | `int` (FK) | |
| `CheckType` | `varchar(15)` | `SCHEDULED`, `ON_DEMAND`, `ON_ERROR` |
| `Status` | `varchar(15)` | `HEALTHY`, `DEGRADED`, `DOWN` |
| `ResponseTimeMs` | `int` | API response time |
| `ErrorMessage` | `nvarchar(500)` | |
| `CheckedAtUtc` | `datetime2` | |

### 5.3 Connector Abstraction

```csharp
public interface IIntegrationConnector
{
    string ProviderCode { get; }
    Task<bool> TestConnectionAsync(TenantIntegration integration, CancellationToken ct);
    Task<TokenRefreshResult> RefreshTokenAsync(TenantIntegration integration, CancellationToken ct);
}
```

Each provider implements this interface. The connector registry maps `ProviderCode` → `IIntegrationConnector`.

**V1 connectors:** `ChannexConnector`, `RazorpayConnector`.
**Future:** `GstPortalConnector`, `ECourtConnector`, etc.

### 5.4 Token Refresh Flow

```
ScheduledJob (every 5 min) → scan IntegrationToken WHERE ExpiresAtUtc < now + 10 min
  → For each: call connector.RefreshTokenAsync()
  → Update IntegrationToken with new encrypted value and expiry
  → Log result in IntegrationHealthLog
  → If refresh fails → set TenantIntegration.Status = 'ERROR'; alert tenant
```

### 5.5 Retry & Error Handling

| Error Type | Retry | Max Attempts | Backoff |
|-----------|-------|-------------|---------|
| Network timeout | Yes | 3 | Exponential (1s, 5s, 15s) |
| 401 Unauthorized | Attempt token refresh, then retry | 1 + 1 | Immediate after refresh |
| 429 Rate Limited | Yes | 3 | Wait for `Retry-After` header |
| 5xx Server Error | Yes | 3 | Exponential (2s, 10s, 30s) |
| 4xx Client Error (non-401) | No | — | Log and alert |

---

## 6. Atlas Automations (Platform Engine)

### 6.1 Design Goal

A **vertical-agnostic** event-driven and time-driven automation engine. PMS uses it for booking lifecycle. Legal uses it for case deadline reminders. CA uses it for filing due dates.

### 6.2 Core Components

```
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ Event Source  │───►│ Rule Engine  │───►│ Action       │
│ (OutboxMsg)   │    │ (Evaluator)  │    │ Executor     │
└──────────────┘    └──────┬───────┘    └──────┬───────┘
                           │                    │
                    ┌──────▼───────┐    ┌──────▼───────┐
                    │ Condition    │    │ Execution    │
                    │ Engine       │    │ Log          │
                    └──────────────┘    └──────────────┘
```

### 6.3 Event Registry

**Vertical-agnostic event model:**

| Field | Description |
|-------|-------------|
| `EventType` | Dot-notated string: `{domain}.{entity}.{action}` |
| `Domain prefix` | `core.*` for platform events; `pms.*` for PMS; `legal.*` for Legal; etc. |

**Platform-level events (Core):**

| Event | Trigger |
|-------|---------|
| `core.tenant.created` | New tenant signup |
| `core.tenant.activated` | Tenant verification complete |
| `core.tenant.suspended` | Billing/abuse suspension |
| `core.subscription.activated` | Plan activated |
| `core.subscription.expiring` | Trial/plan expiring in X days |
| `core.subscription.suspended` | Subscription suspended |
| `core.invoice.created` | New invoice generated |
| `core.invoice.overdue` | Invoice past due date |
| `core.integration.error` | Integration health check failed |
| `core.user.invited` | User invitation sent |
| `core.user.activated` | User accepted invitation |

**Product-level events** are registered by verticals at startup (see §11).

### 6.4 Trigger Model

**Existing `AutomationSchedule`** handles time-based triggers. The entity currently has `BookingId` (PMS-specific). To make it vertical-agnostic:

**New Core fields on `AutomationSchedule`:**

| Field | Type | Description |
|-------|------|-------------|
| `EntityType` | `varchar(30)` | Replaces PMS-specific `BookingId`. e.g., `Booking`, `Case`, `Filing`, `Tenant` |
| `EntityId` | `varchar(50)` | Generic entity reference (string to support any PK type) |

> **CORE-AUT-001**: `BookingId` is retained for backward compatibility. New automation schedule entries use `EntityType` + `EntityId`. A migration step populates `EntityType = 'Booking'` and `EntityId = BookingId.ToString()` for existing rows.

**Trigger types:**

| Type | Mechanism |
|------|-----------|
| **Event-based** | `OutboxMessage` with matching `EventType` triggers rule evaluation |
| **Time-based** | `AutomationSchedule` with `DueAtUtc` fires via `AutomationSchedulerHostedService` |
| **Metric-based** | V2: Threshold breach on a metric triggers rule |

### 6.5 Rule Evaluation Engine

Rules are stored in `AutomationRule` (defined in RA-AUTO-001, extended for Core):

| Extension | Description |
|-----------|-------------|
| `VerticalCode` | `varchar(10)` on `AutomationRule` — which vertical this rule belongs to. `CORE` for platform rules |
| `Module` | `varchar(20)` — further categorization (e.g., `housekeeping`, `retention`, `case_mgmt`) |

Evaluation flow:
1. Event fires (or schedule triggers)
2. Query `AutomationRule WHERE IsActive = true AND TriggerEventType = @eventType AND (TenantId = @tenantId OR TenantId IS NULL)`
3. Evaluate `ConditionsJson` against event payload
4. Execute matching actions
5. Log in `AutomationExecutionLog`

### 6.6 Action Registry

**Platform actions (vertical-agnostic):**

| Action Type | Code | Description |
|------------|------|-------------|
| Send notification | `SEND_NOTIFICATION` | Send WhatsApp/SMS/Email via notification engine |
| Create schedule | `CREATE_SCHEDULE` | Create a future `AutomationSchedule` entry |
| Update entity | `UPDATE_ENTITY` | Generic status update (via registered handler) |
| Create audit log | `CREATE_AUDIT_LOG` | Log an action for compliance |
| Webhook call | `CALL_WEBHOOK` | V2: Call an external URL |

**Product-specific actions** (e.g., `CREATE_HOUSEKEEPING_TASK`, `CREATE_CASE_REMINDER`) are registered by verticals.

### 6.7 Idempotency

| Mechanism | Implementation |
|-----------|---------------|
| Event dedup | `ConsumedEvent` table: `(EventId, ConsumerName)` unique constraint |
| Schedule dedup | `AutomationSchedule` checked before creating duplicate: `(TenantId, EntityType, EntityId, EventType)` |
| Execution dedup | `AutomationExecutionLog.IdempotencyKey` prevents re-execution |

---

## 7. Atlas Notifications (Communication Engine)

### 7.1 Design Goal

A vertical-agnostic, multi-channel notification engine. PMS sends booking confirmations. Legal sends hearing reminders. CA sends filing deadline alerts.

### 7.2 Existing Infrastructure

| Entity | Purpose | Core Status |
|--------|---------|-------------|
| `MessageTemplate` | Templates with event type, channel, scope, language, body | ✓ Exists — vertical-agnostic already |
| `CommunicationLog` | Delivery tracking with idempotency | ✓ Exists — vertical-agnostic already |
| `OutboxMessage` | Transactional outbox for at-least-once delivery | ✓ Exists — vertical-agnostic already |

> **CORE-NOT-001**: The existing notification infrastructure is already well-designed for multi-vertical use. `EventType` is a string (e.g., `booking.confirmed`, `case.hearing_reminder`) — no schema change needed to support new verticals.

### 7.3 Channel Abstraction

```csharp
public interface INotificationChannel
{
    string ChannelCode { get; }  // "WHATSAPP", "SMS", "EMAIL"
    Task<SendResult> SendAsync(NotificationRequest request, CancellationToken ct);
    Task<DeliveryStatus> GetStatusAsync(string providerMessageId, CancellationToken ct);
}
```

**V1 channels:** `WhatsAppChannel`, `SmsChannel`, `EmailChannel`.
**Future:** `PushNotificationChannel`, `InAppChannel`.

### 7.4 Provider Abstraction

```csharp
public interface INotificationProvider
{
    string ProviderCode { get; }   // "twilio", "msg91", "sendgrid"
    string[] SupportedChannels { get; }
    Task<ProviderSendResult> SendAsync(ProviderMessage message, CancellationToken ct);
}
```

Provider selection: `Channel → preferred provider for tenant → fallback provider`.

### 7.5 Template Engine

| Feature | Design |
|---------|--------|
| **Variable substitution** | Handlebars-style `{{variable}}`. Variables resolved from event payload |
| **Conditional blocks** | `{{#if variable}}...{{/if}}` — simple conditionals |
| **Template resolution** | Scope priority: `Property > Tenant > Platform` (for PMS); generic: `Entity > Tenant > Platform` |
| **Versioning** | `TemplateVersion` integer; only `IsActive = true` version is used |
| **Multi-language** | `Language` field on `MessageTemplate`. V1: English. V2: Hindi, Kannada, etc. |

### 7.6 Rate Limiting

| Level | Limit | Scope |
|-------|-------|-------|
| Per recipient | 10 messages per hour (all channels combined) | Per `ToAddress` |
| Per tenant per day | 2,000 messages | Per `TenantId` per calendar day |
| Per channel per tenant | WhatsApp: 1,000/day; SMS: 500/day; Email: 5,000/day | Platform defaults; adjustable per plan |

### 7.7 Opt-Out Management

| Feature | Implementation |
|---------|---------------|
| Channel opt-out | `GuestConsent` / `ContactConsent` entity per recipient per channel |
| Transactional vs marketing | Transactional messages bypass marketing opt-out |
| Opt-out keywords | WhatsApp/SMS: "STOP" triggers opt-out via inbound handler |
| Re-opt-in | Explicit "START" message or portal action |

---

## 8. Atlas Sites (Multi-Tenant Public Site Framework)

### 8.1 Design Goal

Every tenant gets a branded public-facing site. For PMS: property listing and booking page. For Legal: lawyer profile and consultation booking. For CA: firm profile and service catalog.

### 8.2 Routing

| Route Type | Pattern | Resolution |
|-----------|---------|-----------|
| **Slug-based** | `{{slug}}.atlashomestays.com` / `{{slug}}.atlas.law` / `{{slug}}.atlas.ca` | `Tenant.Slug` lookup |
| **Custom domain** | `www.sunsetvilla.in` | `Tenant.CustomDomain` lookup via Cloudflare DNS |
| **Fallback** | `atlashomestays.com/t/{{slug}}` | Path-based for shared domain |

> **CORE-SITE-001**: Site routing is resolved at the CDN edge (Cloudflare Workers) or at the React app level. The API uses `TenantId` from JWT for authenticated requests and slug resolution for public requests.

### 8.3 Public Page Templates

| Feature | V1 | V2 |
|---------|----|----|
| Page structure | Fixed templates per vertical (PMS: property page; Legal: lawyer profile) | Customizable blocks (CMS-lite) |
| Content source | Database entities (Property, Listing, etc.) | Database + custom content blocks |
| SEO metadata | `<title>`, `<meta description>`, OpenGraph tags from tenant config | Structured data (JSON-LD), sitemap |
| Styling | Tenant `BrandColor` + `LogoUrl` applied to template | Full theme customization |

### 8.4 Site Configuration (Per Tenant)

Stored in `Tenant.ConfigJson` under `site` key:

```json
{
  "site": {
    "heroImageUrl": "https://...",
    "tagline": "Your home away from home",
    "showPricing": true,
    "showReviews": true,
    "analyticsId": "G-XXXXXXX",
    "socialLinks": {
      "instagram": "https://...",
      "facebook": "https://..."
    },
    "customCss": ""
  }
}
```

### 8.5 Security Isolation

| Concern | Mitigation |
|---------|-----------|
| Tenant A accessing Tenant B's site admin | JWT `tid` claim enforced; site admin only accessible to own tenant |
| Public page data leakage | Public endpoints return only published/active data; no draft or internal data |
| XSS via custom content | `customCss` sanitized; no `<script>` injection; CSP headers enforced |
| Token-based public flows | Guest portal tokens (RA-GUEST-001) are HMAC-signed and time-limited |

### 8.6 Feature Toggles

| Toggle | Effect |
|--------|--------|
| `site.booking_enabled` | Show/hide booking widget on public site |
| `site.reviews_enabled` | Show/hide guest reviews |
| `site.pricing_enabled` | Show/hide pricing |
| `site.contact_form_enabled` | Show/hide inquiry form |

---

## 9. Atlas Insights (Analytics Foundation)

### 9.1 Design Goal

A vertical-agnostic analytics foundation that provides: event storage, scheduled snapshot generation, tenant-level reporting, and admin-level aggregation — without requiring an external BI tool.

### 9.2 Event Storage Model

All domain events are already stored in `OutboxMessage`. For analytics, a **denormalized event table** is introduced:

**`AnalyticsEvent`**

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `long` PK | |
| `TenantId` | `int` | |
| `EventType` | `varchar(80)` | `pms.booking.created`, `legal.case.filed`, etc. |
| `EntityType` | `varchar(30)` | `Booking`, `Case`, `Filing` |
| `EntityId` | `varchar(50)` | |
| `EventDataJson` | `nvarchar(max)` | Flattened event properties for querying |
| `OccurredAtUtc` | `datetime2` | |
| `CreatedAtUtc` | `datetime2` | |

> **CORE-INS-001**: `AnalyticsEvent` is populated by a background job that reads published `OutboxMessage` entries and denormalizes them. This decouples analytics from the transactional outbox.

### 9.3 Snapshot Generation Framework

| Component | Description |
|-----------|-------------|
| **`ISnapshotGenerator`** | Interface: `Task GenerateAsync(int tenantId, DateOnly snapshotDate, CancellationToken ct)` |
| **Vertical registration** | Each vertical registers its snapshot generators at startup (e.g., `DailyPropertyPerformanceSnapshotGenerator` for PMS) |
| **Scheduler** | `SnapshotSchedulerJob` runs at 02:00 UTC daily. Iterates all active tenants. Calls registered generators |
| **Idempotency** | Snapshots are upserted with `(TenantId, SnapshotDate)` unique constraint. Re-running is safe |

### 9.4 Cross-Tenant Aggregation

| Rule | Access |
|------|--------|
| Tenant can only see own data | Enforced by `ITenantOwnedEntity` filter |
| Admin aggregation queries | `core.admin` role; queries use `.IgnoreQueryFilters()` with aggregate-only projection (no PII) |
| Export | Per-tenant CSV/JSON export. Admin can export aggregated metrics (anonymized) |

### 9.5 Future NLQ Interface

| Feature | V1 | V2 |
|---------|----|----|
| Query interface | Pre-built dashboard widgets, filtered views | Natural language query → SQL (with guardrails) |
| Supported questions | "What are my top properties by revenue?" | "Show me occupancy trends for the last 6 months for properties in Goa" |
| Guardrails | N/A | Query cost limit, row count cap, no DDL/DML, tenant filter always injected |

---

## 10. Audit & Compliance Layer

### 10.1 AuditLog Entity (Existing)

The existing `AuditLog` is already well-designed for Core:

| Field | Purpose | Core Usage |
|-------|---------|-----------|
| `TenantId` | Tenant scope | All audit entries are tenant-scoped |
| `ActorUserId` | Who performed the action | NULL for system actions |
| `Action` | What was done | Dot-notated: `booking.created`, `tenant.suspended`, `role.assigned` |
| `EntityType` | What entity was affected | `Booking`, `Tenant`, `User`, etc. |
| `EntityId` | Which instance | ID of the affected entity |
| `TimestampUtc` | When | UTC timestamp |
| `PayloadJson` | Before/after diff | JSON with redacted sensitive fields |

### 10.2 Sensitive Action Tracking

| Action Category | Examples | Audit Level |
|----------------|---------|-------------|
| **Authentication** | Login, logout, failed login, password reset | Always |
| **Authorization** | Role assigned, role removed, permission change | Always |
| **Data access** | Admin cross-tenant access, PII export, data deletion | Always |
| **Financial** | Invoice created, payment received, subscription change | Always |
| **Configuration** | Feature flag change, plan change, integration enable/disable | Always |
| **Entity lifecycle** | Create, update, delete of core entities | Configurable (default: on for creates/deletes, off for reads) |

### 10.3 Export Audit Capability

| Feature | Design |
|---------|--------|
| **Tenant audit export** | `GET /api/audit/export?from=2026-01-01&to=2026-03-01` → JSON file. Permission: `owner` only |
| **Admin audit export** | Same endpoint with `core.admin` role → cross-tenant if requested |
| **Format** | JSON lines (one JSON object per line) for streaming |
| **Retention** | `AuditLog` retained for 3 years. Archived to cold storage in V2 |

### 10.4 Admin Access Review

| Feature | Frequency | Implementation |
|---------|-----------|---------------|
| List all admin cross-tenant accesses | On demand | Query `AuditLog WHERE Action = 'admin.cross_tenant_access'` |
| Super-admin action review | Monthly | Aggregated report of all `core.superadmin` actions |
| Inactive user audit | Monthly | Users with no login in 90 days flagged for review |

---

## 11. Multi-Vertical Extensibility Model

### 11.1 Product Module Registration

Each vertical product registers itself at application startup via a fluent API:

```csharp
public interface IVerticalModule
{
    string VerticalCode { get; }         // "PMS", "LEGAL", "CA"
    string DisplayName { get; }          // "Atlas PMS"
    void RegisterEventTypes(EventTypeRegistry registry);
    void RegisterActionTypes(ActionTypeRegistry registry);
    void RegisterRoleTemplates(RoleTemplateRegistry registry);
    void RegisterSnapshotGenerators(SnapshotRegistry registry);
    void RegisterNavigationItems(NavigationRegistry registry);
    void RegisterPlanFeatures(PlanFeatureRegistry registry);
    void ConfigureServices(IServiceCollection services);
    void ConfigureEndpoints(IEndpointRouteBuilder endpoints);
}
```

**Example:**
```csharp
public class PmsModule : IVerticalModule
{
    public string VerticalCode => "PMS";
    public string DisplayName => "Atlas PMS";

    public void RegisterEventTypes(EventTypeRegistry r)
    {
        r.Register("pms.booking.created", "Booking Created");
        r.Register("pms.booking.confirmed", "Booking Confirmed");
        r.Register("pms.stay.checked_out", "Guest Checked Out");
        // ...
    }

    public void RegisterRoleTemplates(RoleTemplateRegistry r)
    {
        r.Register("pms.owner", "Property Owner", new[] { "bookings.*", "listings.*", "reports.*" });
        r.Register("pms.housekeeping", "Housekeeping", new[] { "tasks.own.*" });
        // ...
    }
    // ...
}
```

### 11.2 Feature Registration

Each vertical declares its **features** that can be toggled per plan or per tenant:

```csharp
registry.RegisterFeature("ops.housekeeping.enabled", "Housekeeping Module", FeatureType.Boolean, defaultValue: false);
registry.RegisterFeature("retention.campaigns.enabled", "Retention Campaigns", FeatureType.Boolean, defaultValue: false);
registry.RegisterFeature("connect.channex.enabled", "Channex Integration", FeatureType.Boolean, defaultValue: true);
```

Features are stored in `BillingPlan.FeaturesJson` and evaluated via `IFeatureFlagService.IsEnabled(tenantId, featureCode)`.

### 11.3 Navigation Injection

Each vertical provides navigation items for the admin portal sidebar:

```csharp
registry.AddSection("Operations", "pms", sortOrder: 3, icon: "wrench");
registry.AddItem("Housekeeping", "/ops/housekeeping", permission: "ops.housekeeping.view", section: "Operations");
registry.AddItem("Maintenance", "/ops/maintenance", permission: "ops.maintenance.view", section: "Operations");
```

The admin portal React app renders the sidebar from a `/api/navigation` endpoint that returns items filtered by the user's roles and the tenant's active vertical.

### 11.4 Plan Feature Binding

| Concept | Example |
|---------|---------|
| Plan defines features | `Starter` plan: `ops.housekeeping.enabled = true, retention.campaigns.enabled = false` |
| Feature check | API middleware checks `IFeatureFlagService.IsEnabled(tenantId, "retention.campaigns.enabled")` before allowing access to campaign endpoints |
| Upgrade prompt | If feature is disabled, API returns 403 with `{ "code": "FEATURE_REQUIRES_UPGRADE", "feature": "retention.campaigns.enabled", "requiredPlan": "Growth" }` |

### 11.5 Boundaries

| Layer | Owns | Example |
|-------|------|---------|
| **Core schema** | Tenants, Users, Roles, Permissions, Subscriptions, Plans, Invoices, Payments, Credits, Integrations, Outbox, CommunicationLog, MessageTemplate, AutomationSchedule, AutomationRule, AuditLog, AnalyticsEvent | Vertical-agnostic |
| **Product schema** | Entities specific to the vertical | PMS: Bookings, Listings, Properties, Guests, HousekeepingTask, etc. |
| **Shared abstractions** | Interfaces and base classes in Core that verticals implement | `IVerticalModule`, `ISnapshotGenerator`, `IIntegrationConnector`, `INotificationChannel` |

---

## 12. Database Schema Separation Strategy

### 12.1 Logical Schema Naming

| Schema | Purpose | Examples |
|--------|---------|---------|
| `core` | Platform entities | `core.Tenant`, `core.User`, `core.Role`, `core.BillingPlan`, `core.OutboxMessage` |
| `pms` | PMS-specific entities | `pms.Booking`, `pms.Listing`, `pms.Property`, `pms.Guest`, `pms.HousekeepingTask` |
| `legal` | Legal vertical entities | `legal.Case`, `legal.Hearing`, `legal.Client` |
| `ca` | CA vertical entities | `ca.Filing`, `ca.Client`, `ca.TaxReturn` |
| `clinic` | Clinic vertical entities | `clinic.Appointment`, `clinic.Patient`, `clinic.Prescription` |

> **CORE-DB-001**: V1 uses logical naming convention (table prefix `Core_`, `Pms_`, etc.) rather than SQL Server schemas. This avoids EF Core schema-per-entity complexity. V2 may move to SQL Server schemas for stronger isolation.

### 12.2 Migration Isolation

| Rule | Implementation |
|------|---------------|
| Core migrations numbered `YYYYMMDD_Core_*` | Core team owns these; run on every deployment |
| Product migrations numbered `YYYYMMDD_{Vertical}_*` | Each vertical owns its migrations |
| Cross-schema FK | Product entities can FK to Core entities (e.g., `Pms_Booking.TenantId → Core_Tenant.Id`). Core entities NEVER FK to product entities |
| Migration order | Core migrations always run first; product migrations run after |

### 12.3 Naming Conventions

| Element | Convention | Example |
|---------|-----------|---------|
| Table | `{Prefix}_{PascalCase}` | `Core_Tenant`, `Pms_Booking` |
| Column | `PascalCase` | `TenantId`, `CreatedAtUtc` |
| Index | `IX_{Table}_{Columns}` | `IX_Pms_Booking_TenantId_BookingStatus` |
| Unique constraint | `UQ_{Table}_{Columns}` | `UQ_Core_Role_Code` |
| Foreign key | `FK_{ChildTable}_{ParentTable}_{Column}` | `FK_Pms_Booking_Core_Tenant_TenantId` |

> **CORE-DB-002**: V1 operates with the existing naming (no prefix). The prefix convention is applied **only to new tables** going forward. A migration to rename existing tables to the prefixed convention is deferred to V2.

### 12.4 Indexing Standards

| Rule | Rationale |
|------|-----------|
| Every `ITenantOwnedEntity` table has `TenantId` as the **leading column** in all composite indexes | Tenant-scoped queries are the norm |
| Status columns use filtered indexes (`WHERE Status = 'Active'`) | Most queries filter on active records |
| `datetime2` columns used for range queries have indexes | CreatedAtUtc, DueAtUtc, OccurredAtUtc |
| JSON columns are NOT indexed | Use computed/persisted columns for indexed JSON values (V2) |
| Cover indexes for dashboard queries | Include columns to avoid key lookups for common queries |

### 12.5 TenantId Indexing Rules

| Pattern | Implementation |
|---------|---------------|
| Single-tenant queries (99% of queries) | `(TenantId, ...)` composite index; global query filter ensures tenant scoping |
| Cross-tenant admin queries | Separate admin endpoints with `.IgnoreQueryFilters()`; use non-TenantId-leading indexes sparingly |
| Aggregate queries | Pre-computed in snapshot tables; no cross-tenant OLTP queries |

---

## 13. Performance & Scalability

### 13.1 Tenant Isolation Strategy

| Layer | Mechanism |
|-------|-----------|
| **Database** | All entities have `TenantId`; global EF Core query filter prevents cross-tenant reads |
| **API** | `TenantId` resolved from JWT claim; injected into DbContext at request scope |
| **Cache** | Cache keys prefixed with `tenant:{tenantId}:` to prevent cross-tenant cache pollution |
| **Background jobs** | Jobs iterate tenants and process in per-tenant batches |
| **Rate limiting** | Per-tenant rate limits prevent one tenant from monopolizing resources |

### 13.2 Query Guardrails

| Guardrail | Limit | Enforcement |
|-----------|-------|-------------|
| Max rows per API response | 100 (default), 500 (max with explicit `?pageSize=500`) | API pagination middleware |
| Query timeout | 30 seconds | DbContext command timeout |
| No `SELECT *` in generated queries | EF Core projections (`.Select()`) for list endpoints | Code review + analyzer |
| No cross-tenant JOINs in OLTP | Tenant filter ensures single-tenant scope | Global query filter |
| Dashboard queries use snapshots | Pre-computed daily/monthly aggregate tables | Separate snapshot read service |

### 13.3 Snapshot vs Transactional Reads

| Query Type | Source | Max Latency |
|-----------|--------|-------------|
| Operational (CRUD) | OLTP tables with tenant filter | < 200ms |
| Dashboard widgets | Snapshot tables (pre-computed daily) | < 500ms |
| Reports (date range) | OLTP with appropriate indexes | < 2s |
| Cross-tenant aggregates | Snapshot tables with admin filter | < 5s |
| Export (large dataset) | Streaming from OLTP with cursor pagination | Background job |

### 13.4 Scaling to 100k Tenants

| Concern | Strategy |
|---------|----------|
| Table size | Partitioning by `TenantId` range (V2). V1: indexes sufficient for millions of rows |
| Connection pool | Azure SQL elastic pool with per-tenant connection limits. App uses connection pooling |
| Background jobs | Jobs process tenants in configurable batch sizes (default 50). Stagger via modulo to avoid thundering herd |
| Memory | In-memory caches with per-tenant TTL and eviction. No unbounded caches |
| Compute | Azure App Service scale-up (vertical) for V1. Scale-out (horizontal) with sticky sessions for V2 |

---

## 14. Observability & Platform Health

### 14.1 Platform Health Dashboard (Atlas Admin)

| Panel | Metric | Source |
|-------|--------|--------|
| **Tenants** | Total active, pending, suspended | `Tenant` table |
| **Subscriptions** | Active, trial, past due, suspended | `TenantSubscription` |
| **Integrations** | Healthy, degraded, down | `TenantIntegration.LastHealthStatus` |
| **Automations** | Executions/hour, failure rate, dead-letter count | `AutomationExecutionLog` |
| **Notifications** | Sent/hour, delivery rate, failure rate | `CommunicationLog` |
| **Outbox** | Pending messages, avg processing latency | `OutboxMessage` |
| **API** | Request rate, p50/p95/p99 latency, error rate | Application Insights / structured logs |
| **Database** | DTU usage, query duration p95, deadlocks | Azure SQL metrics |

### 14.2 Alert Thresholds

| Alert | Threshold | Channel |
|-------|-----------|---------|
| Outbox backlog | > 500 pending messages for > 5 min | Admin dashboard + Slack/email |
| Integration health | Any `DOWN` status for > 15 min | Admin dashboard + tenant notification |
| Automation failure rate | > 10% failures in 1 hour | Admin dashboard |
| Notification failure rate | > 5% failures in 1 hour | Admin dashboard |
| Subscription enforcement failure | Tenant should be suspended but isn't | Admin dashboard |
| API error rate | > 2% 5xx errors in 5 min | Admin dashboard |
| Database DTU | > 80% for > 10 min | Admin dashboard |

### 14.3 Logging Standards

| Standard | Implementation |
|----------|---------------|
| **Structured logging** | Serilog with JSON output. Every log entry includes `TenantId`, `UserId`, `CorrelationId` |
| **Correlation ID** | Generated per HTTP request; propagated to all downstream operations and outbox messages |
| **Log levels** | `Error`: unhandled exceptions, data integrity issues. `Warning`: retry, degraded state. `Information`: business events. `Debug`: query details |
| **PII redaction** | Phone/email masked in logs. Full values only in `AuditLog.PayloadJson` (encrypted at rest) |
| **Retention** | Application logs: 30 days. Structured events: 90 days. AuditLog: 3 years |

---

## 15. Security Model

### 15.1 Token Encryption

| Token Type | Storage | Encryption |
|-----------|---------|-----------|
| JWT access token | Client memory (JS variable) | Signed with RS256 (asymmetric) |
| JWT refresh token | HttpOnly secure cookie | Hashed (bcrypt) in `User.RefreshTokenHash` |
| Integration tokens | `IntegrationToken.EncryptedValue` | AES-256-GCM with per-environment key |
| Tenant config secrets | `Tenant.ConfigJson` (sensitive fields) | AES-256 with per-environment key |
| Vendor bank info | `Vendor.BankAccountInfo` (RA-OPS-001) | AES-256 with per-environment key |
| PAN hash | `TenantProfile.PanHash` | BCrypt one-way hash |

> **CORE-SEC-001**: Encryption keys are stored in Azure Key Vault (production) or `appsettings.Development.json` (local). Keys are rotated annually. Re-encryption job runs after rotation.

### 15.2 Authorization Enforcement

| Layer | Mechanism |
|-------|-----------|
| **Route** | `[Authorize]` attribute with role requirements |
| **Permission** | `[RequirePermission("code")]` custom attribute |
| **Tenant status** | `[RequireTenantActive]` middleware |
| **Subscription status** | `[RequireSubscriptionActive]` middleware |
| **Feature flag** | `[RequireFeature("feature.code")]` middleware |
| **Data** | `ITenantOwnedEntity` global query filter |

### 15.3 Input Validation

| Rule | Implementation |
|------|---------------|
| All request DTOs validated | Data annotations + FluentValidation |
| SQL injection prevention | EF Core parameterized queries (never raw SQL with string concatenation) |
| XSS prevention | Output encoding in API responses; CSP headers on sites |
| File upload validation | V2: MIME type check, size limit, virus scan |
| JSON depth limit | Max 10 levels of nesting in request bodies |

### 15.4 Rate Limiting

| Scope | Limit | Implementation |
|-------|-------|---------------|
| Per IP (unauthenticated) | 60 requests/min | ASP.NET Core rate limiting middleware |
| Per user (authenticated) | 300 requests/min | JWT `sub` claim as key |
| Per tenant | 1,000 requests/min | JWT `tid` claim as key |
| Login endpoint | 5 attempts/min per email | IP + email composite key |
| Webhook endpoints | 100 requests/min per provider | Provider-specific key |

### 15.5 Tenant Boundary Enforcement

| Rule | Implementation |
|------|---------------|
| No tenant A data in tenant B response | Global query filter; integration tests verify |
| No tenant ID spoofing | `TenantId` from JWT claim, never from request body |
| Admin cross-tenant access | Requires `core.admin` role; fully audited |
| Public endpoints | Slug-based resolution; return only public/published data |

### 15.6 Sensitive Config Encryption

| Config Type | Storage | Encryption |
|------------|---------|-----------|
| Integration API keys | `IntegrationToken.EncryptedValue` | AES-256-GCM |
| Webhook secrets | `Tenant.ConfigJson.webhookSecret` | AES-256 |
| SMTP credentials | App configuration (Azure Key Vault) | Platform-level, not per-tenant |
| Payment gateway keys | App configuration (Azure Key Vault) | Platform-level |

---

## 16. Definition of Done — Atlas Core V1

### 16.1 Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | Tenant creation with `PENDING_VERIFICATION` → `ACTIVE` lifecycle | ☐ |
| 2 | Tenant suspension on billing failure with read-only mode | ☐ |
| 3 | Tenant soft-delete with data retention | ☐ |
| 4 | Tenant `ConfigJson` stores feature flags, limits, preferences | ☐ |
| 5 | `VerticalCode` on Tenant determines active product module | ☐ |
| 6 | User CRUD with `ACTIVE`, `INVITED`, `DISABLED` lifecycle | ☐ |
| 7 | Role and Permission entities seeded; `RoleAssignment` many-to-many | ☐ |
| 8 | JWT authentication with access + refresh token rotation | ☐ |
| 9 | Route-level authorization with `[RequirePermission]` attribute | ☐ |
| 10 | Subscription enforcement middleware (soft/hard block) | ☐ |
| 11 | Plan `FeaturesJson` and `LimitsJson` drive feature flags and usage caps | ☐ |
| 12 | Plan upgrade/downgrade flow with proration | ☐ |
| 13 | Invoice generation and Razorpay payment integration | ☐ |
| 14 | `IntegrationProvider` / `TenantIntegration` / `IntegrationToken` CRUD | ☐ |
| 15 | Token encryption at rest (AES-256-GCM) for integration tokens | ☐ |
| 16 | Integration health monitoring with scheduled checks | ☐ |
| 17 | `AutomationSchedule` extended with `EntityType` + `EntityId` (vertical-agnostic) | ☐ |
| 18 | `AutomationRule` supports `VerticalCode` for module isolation | ☐ |
| 19 | Event registry accepts vertical-registered event types | ☐ |
| 20 | Action registry accepts vertical-registered action types | ☐ |
| 21 | Notification engine sends via WhatsApp/SMS/Email with channel fallback | ☐ |
| 22 | `MessageTemplate` supports any vertical's event types (already generic) | ☐ |
| 23 | Rate limiting on notifications (per-recipient, per-tenant) | ☐ |
| 24 | Opt-out management functional | ☐ |
| 25 | Tenant slug routing for public sites | ☐ |
| 26 | Custom domain support (DNS-level, Cloudflare) | ☐ |
| 27 | Public page serves tenant-branded content | ☐ |
| 28 | `AnalyticsEvent` populated from outbox | ☐ |
| 29 | Snapshot scheduler runs daily; verticals register generators | ☐ |
| 30 | AuditLog captures all sensitive actions | ☐ |
| 31 | Audit export endpoint functional | ☐ |
| 32 | `IVerticalModule` interface defined; PMS registered as first vertical | ☐ |
| 33 | Navigation injection renders vertical-specific sidebar items | ☐ |

### 16.2 Non-Functional Checklist

| # | Requirement | Status |
|---|-------------|--------|
| 1 | All `ITenantOwnedEntity` tables have global query filter | ☐ |
| 2 | Cross-tenant leakage tests pass for all Core entities | ☐ |
| 3 | Cross-tenant leakage tests pass for all PMS entities | ☐ |
| 4 | JWT token validation < 5ms | ☐ |
| 5 | Permission resolution < 10ms (cached) | ☐ |
| 6 | Subscription enforcement middleware < 5ms overhead | ☐ |
| 7 | Integration token encryption/decryption < 10ms | ☐ |
| 8 | Notification rate limiting functional | ☐ |
| 9 | Core namespace has zero references to PMS/vertical namespaces | ☐ |
| 10 | All migrations are additive (no data loss) | ☐ |
| 11 | Database schema follows naming conventions (new tables) | ☐ |
| 12 | Structured logging with TenantId + CorrelationId on every entry | ☐ |
| 13 | API rate limiting enforced at all tiers (IP, user, tenant) | ☐ |
| 14 | PII redacted in application logs | ☐ |
| 15 | Platform health dashboard functional for admin | ☐ |
| 16 | Alert thresholds configured and verified | ☐ |
| 17 | Single-developer maintainability: no abstraction requires > 5 min to understand | ☐ |

---

## Appendix A — Existing Entity Mapping

Current entities and their Core classification:

| Entity | Current Namespace | Core / Product | Migration Action |
|--------|------------------|---------------|-----------------|
| `Tenant` | `Atlas.Api.Models` | **Core** | Add `VerticalCode`, `Status`, `ConfigJson`, `Timezone`, `Country` |
| `TenantProfile` | `Atlas.Api.Models` | **Core** | No change |
| `User` | `Atlas.Api.Models` | **Core** | Add `Status`, `LastLoginAtUtc`, `RefreshTokenHash`, etc. |
| `AuditLog` | `Atlas.Api.Models` | **Core** | No change |
| `OutboxMessage` | `Atlas.Api.Models` | **Core** | No change |
| `AutomationSchedule` | `Atlas.Api.Models` | **Core** | Add `EntityType`, `EntityId` |
| `MessageTemplate` | `Atlas.Api.Models` | **Core** | No change (already generic) |
| `CommunicationLog` | `Atlas.Api.Models` | **Core** | No change (already generic) |
| `BillingPlan` | `Atlas.Api.Models.Billing` | **Core** | Add `VerticalCode`, `FeaturesJson`, `LimitsJson`, `TrialDays`, etc. |
| `TenantSubscription` | `Atlas.Api.Models.Billing` | **Core** | No change |
| `BillingInvoice` | `Atlas.Api.Models.Billing` | **Core** | No change |
| `BillingPayment` | `Atlas.Api.Models.Billing` | **Core** | No change |
| `TenantCreditsLedger` | `Atlas.Api.Models.Billing` | **Core** | No change |
| `TenantPricingSetting` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Booking` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Listing` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Property` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Guest` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Payment` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |
| `Incident` | `Atlas.Api.Models` | **PMS product** | Move to PMS namespace (V2) |

> **CORE-MAP-001**: V1 does NOT move existing entities to new namespaces. The mapping is documented for planning. V2 introduces the namespace separation as a non-breaking refactor (namespace + using aliases).

---

## Appendix B — Vertical Registration Example

How a hypothetical "Atlas Legal" vertical would register with Core:

```csharp
public class LegalModule : IVerticalModule
{
    public string VerticalCode => "LEGAL";
    public string DisplayName => "Atlas Legal";

    public void RegisterEventTypes(EventTypeRegistry r)
    {
        r.Register("legal.case.created", "New Case Filed");
        r.Register("legal.hearing.scheduled", "Hearing Scheduled");
        r.Register("legal.hearing.reminder_due", "Hearing Reminder Due");
        r.Register("legal.filing.deadline", "Filing Deadline Approaching");
    }

    public void RegisterActionTypes(ActionTypeRegistry r)
    {
        r.Register("CREATE_CASE_REMINDER", typeof(CreateCaseReminderHandler));
        r.Register("SEND_HEARING_NOTICE", typeof(SendHearingNoticeHandler));
    }

    public void RegisterRoleTemplates(RoleTemplateRegistry r)
    {
        r.Register("legal.partner", "Partner", new[] { "cases.*", "clients.*", "reports.*", "billing.*" });
        r.Register("legal.associate", "Associate", new[] { "cases.own.*", "clients.view" });
        r.Register("legal.clerk", "Clerk", new[] { "cases.view", "filings.create" });
    }

    public void RegisterSnapshotGenerators(SnapshotRegistry r)
    {
        r.Register<DailyCasePerformanceSnapshotGenerator>();
    }

    public void RegisterNavigationItems(NavigationRegistry r)
    {
        r.AddSection("Case Management", "legal", sortOrder: 1, icon: "briefcase");
        r.AddItem("Cases", "/cases", permission: "cases.view", section: "Case Management");
        r.AddItem("Hearings", "/hearings", permission: "hearings.view", section: "Case Management");
        r.AddItem("Clients", "/clients", permission: "clients.view", section: "Case Management");
    }

    public void RegisterPlanFeatures(PlanFeatureRegistry r)
    {
        r.RegisterFeature("legal.ecourt.enabled", "eCourt Integration", default: false);
        r.RegisterFeature("legal.document_mgmt.enabled", "Document Management", default: true);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IHearingService, HearingService>();
    }

    public void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapControllers(); // Legal controllers auto-discovered
    }
}
```

This module would be registered in `Program.cs`:
```csharp
builder.Services.AddVerticalModule<PmsModule>();    // existing
builder.Services.AddVerticalModule<LegalModule>();  // new
```

The Core startup pipeline calls each module's registration methods, building a unified event registry, action registry, role set, navigation tree, and feature catalog — all without Core having any compile-time dependency on the vertical module's internal types.

---

*End of RA-CORE-001*
