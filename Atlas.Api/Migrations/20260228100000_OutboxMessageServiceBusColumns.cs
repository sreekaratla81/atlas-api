using Atlas.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Atlas.Api.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260228100000_OutboxMessageServiceBusColumns")]
    public partial class OutboxMessageServiceBusColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Topic",
                table: "OutboxMessage",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "booking.events");

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "OutboxMessage",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptUtc",
                table: "OutboxMessage",
                type: "datetime",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "OutboxMessage",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "OutboxMessage",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OccurredUtc",
                table: "OutboxMessage",
                type: "datetime",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "OutboxMessage",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE OutboxMessage SET
                    EntityId = COALESCE(EntityId, AggregateId),
                    OccurredUtc = COALESCE(OccurredUtc, CreatedAtUtc),
                    Status = CASE WHEN PublishedAtUtc IS NOT NULL THEN 'Published' ELSE 'Pending' END,
                    NextAttemptUtc = CASE WHEN PublishedAtUtc IS NULL THEN CreatedAtUtc ELSE NULL END;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "AggregateType",
                table: "OutboxMessage",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "AggregateId",
                table: "OutboxMessage",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_Status_NextAttemptUtc",
                table: "OutboxMessage",
                columns: new[] { "Status", "NextAttemptUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_TenantId_CreatedAtUtc",
                table: "OutboxMessage",
                columns: new[] { "TenantId", "CreatedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_OutboxMessage_Status_NextAttemptUtc", table: "OutboxMessage");
            migrationBuilder.DropIndex(name: "IX_OutboxMessage_TenantId_CreatedAtUtc", table: "OutboxMessage");

            migrationBuilder.DropColumn(name: "Topic", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "Status", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "NextAttemptUtc", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "CorrelationId", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "EntityId", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "OccurredUtc", table: "OutboxMessage");
            migrationBuilder.DropColumn(name: "SchemaVersion", table: "OutboxMessage");

            migrationBuilder.AlterColumn<string>(
                name: "AggregateType",
                table: "OutboxMessage",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AggregateId",
                table: "OutboxMessage",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
