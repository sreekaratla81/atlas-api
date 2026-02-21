# Migrations Troubleshooting

## “Constraint not found” during `Update-Database`

A common failure during `Update-Database` is a SQL Server error like:

```
'AK_BankAccounts_TempId' is not a constraint.
```

This typically happens when a prior migration (or manual database change) already dropped or renamed a constraint or index, but the generated migration still attempts to drop it unconditionally.

## Recommended fix: repair migration with safe `IF EXISTS` checks

Create a new **repair migration** that uses SQL Server `IF EXISTS` checks to drop constraints and indexes only if they are present. This keeps migrations idempotent and prevents failures in environments that drifted from the expected schema.

### Example snippet

```sql
-- Drop a key constraint only if it exists
IF EXISTS (
    SELECT 1
    FROM sys.key_constraints kc
    WHERE kc.name = 'AK_BankAccounts_TempId'
)
BEGIN
    ALTER TABLE dbo.BankAccounts
    DROP CONSTRAINT [AK_BankAccounts_TempId];
END

-- Drop an index only if it exists
IF EXISTS (
    SELECT 1
    FROM sys.indexes i
    WHERE i.name = 'IX_BankAccounts_TempId'
      AND i.object_id = OBJECT_ID('dbo.BankAccounts')
)
BEGIN
    DROP INDEX [IX_BankAccounts_TempId] ON dbo.BankAccounts;
END
```

## How to run

```
dotnet ef database update
```

## “Invalid column name 'Inventory'” during API usage

If you see `Invalid column name 'Inventory'`, it indicates schema drift between the database and the expected model. Resolve it by running the DbMigrator; it is the only supported migration path (do not apply migrations on API startup). Use the exact command below with your connection string:

```
dotnet run --project Atlas.DbMigrator -- --connection "$env:ATLAS_DB_CONNECTION"
```

## “Invalid object name 'Tenants'” in production (500 on /listings)

This means the **production database has not had EF migrations applied** (or was created before tenant support). The API’s `TenantProvider` queries the `Tenants` table on every request; if the table is missing, you get a 500.

**Fix: apply migrations to the prod database.**

- **Option A – GitHub Actions:** Run the **Build and Deploy to Prod** workflow via **workflow_dispatch**, and choose environment **prod**. That run will check for pending migrations and apply them, then deploy.
- **Option B – Local:** With the prod connection string (e.g. from Azure or secrets), run:
  ```bash
  dotnet run --project Atlas.DbMigrator -- --connection "<ATLAS_PROD_SQL_CONNECTION_STRING>"
  ```

**Note:** On a normal **push** to `main`, the deploy workflow does **not** run the migration step; only a **manual** workflow run (workflow_dispatch) applies migrations. So after adding new tables or columns, either trigger the prod workflow manually once or run DbMigrator against prod as above.

## PendingModelSync / CalendarPricingDto: "Cannot find the object CalendarPricingDto" in integration tests

The migration `20260220132813_PendingModelSync` previously tried to ALTER table `CalendarPricingDto` unconditionally. On a fresh test DB that table may not exist yet, so the ALTER fails. The fix: PendingModelSync wraps the ALTER in `IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CalendarPricingDto')` so it only runs when the table exists. This allows migrations to apply successfully on both fresh DBs and DBs with the table. Run integration tests locally (or the atlas-e2e release gate) to validate migrations before pushing.

## Avoid RestoreSnapshot-style migrations (duplicate CreateTable)

When `AppDbContextModelSnapshot` gets out of sync with the migration history, running `dotnet ef migrations add RestoreSnapshot` (or similar) can cause EF to generate a migration that **recreates the entire schema**—`CreateTable` for all tables. Since `InitialBaseline` already creates those tables, applying such a migration fails with "There is already an object named 'X' in the database." This anti-pattern has occurred multiple times.

**Correct approach when snapshot is out of sync:**
1. Run `dotnet ef migrations add SyncSnapshot` (or a descriptive name).
2. Inspect the generated migration.
3. If it contains `CreateTable` for tables that `InitialBaseline` creates, **clear the `Up()` method**—make it empty. The Designer file updates the snapshot; that's all you need.
4. See `20260216034208_SyncModelSnapshot.cs` for an example of an intentionally empty migration.

**Tables created by InitialBaseline** (do not re-create these in later migrations): AutomationSchedule, BankAccounts, EnvironmentMarker, Guests, Incidents, MessageTemplate, OutboxMessage, Properties, Users, Listings, Bookings, ListingPricing, AvailabilityBlock, CommunicationLog, Payments, ListingDailyRate.
