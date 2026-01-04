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
