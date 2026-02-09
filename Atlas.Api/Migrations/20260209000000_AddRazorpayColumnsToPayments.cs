using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRazorpayColumnsToPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add RazorpayOrderId column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpayOrderId')
                BEGIN
                    ALTER TABLE [Payments] 
                    ADD [RazorpayOrderId] nvarchar(100) NULL;
                END
            ");

            // Add RazorpayPaymentId column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpayPaymentId')
                BEGIN
                    ALTER TABLE [Payments] 
                    ADD [RazorpayPaymentId] nvarchar(100) NULL;
                END
            ");

            // Add RazorpaySignature column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpaySignature')
                BEGIN
                    ALTER TABLE [Payments] 
                    ADD [RazorpaySignature] nvarchar(200) NULL;
                END
            ");

            // Add Status column if it doesn't exist
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'Status')
                BEGIN
                    ALTER TABLE [Payments] 
                    ADD [Status] nvarchar(20) NOT NULL DEFAULT 'pending';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove columns if they exist
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpayOrderId')
                BEGIN
                    ALTER TABLE [Payments] DROP COLUMN [RazorpayOrderId];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpayPaymentId')
                BEGIN
                    ALTER TABLE [Payments] DROP COLUMN [RazorpayPaymentId];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'RazorpaySignature')
                BEGIN
                    ALTER TABLE [Payments] DROP COLUMN [RazorpaySignature];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns 
                    WHERE object_id = OBJECT_ID(N'[dbo].[Payments]') 
                    AND name = 'Status')
                BEGIN
                    ALTER TABLE [Payments] DROP COLUMN [Status];
                END
            ");
        }
    }
}
