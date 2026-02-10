using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Atlas.Api.Data;

#nullable disable

namespace Atlas.Api.Migrations
{
    [Migration("20260204064128_AddRazorpayPaymentFields")]
    [DbContext(typeof(AppDbContext))]
    public partial class AddRazorpayPaymentFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
