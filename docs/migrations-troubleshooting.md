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
