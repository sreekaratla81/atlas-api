using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class Repair_BankAccounts_ConstraintDrop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'AK_BankAccounts_TempId'
        AND parent_object_id = OBJECT_ID(N'[dbo].[BankAccounts]')
)
BEGIN
    ALTER TABLE [dbo].[BankAccounts] DROP CONSTRAINT [AK_BankAccounts_TempId];
END

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'AK_BankAccounts_TempId'
        AND object_id = OBJECT_ID(N'[dbo].[BankAccounts]')
)
BEGIN
    DROP INDEX [AK_BankAccounts_TempId] ON [dbo].[BankAccounts];
END
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
