using GhoghnoosAcc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace GhoghnoosAcc.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261126090000_AddInvoiceLineDiscount")]
public sealed class AddInvoiceLineDiscount : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "Discount",
            table: "InvoiceLines",
            type: "numeric(18,2)",
            nullable: false,
            defaultValue: 0m);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Discount", table: "InvoiceLines");
    }
}
