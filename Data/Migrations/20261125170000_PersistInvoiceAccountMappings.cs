using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhoghnoosAcc.Data.Migrations;

public partial class PersistInvoiceAccountMappings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(name: "CounterpartyAccountId", table: "Invoices", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "OperatingAccountId", table: "Invoices", type: "integer", nullable: true);
        migrationBuilder.AddColumn<int>(name: "TaxAccountId", table: "Invoices", type: "integer", nullable: true);

        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_CounterpartyAccountId", table: "Invoices", columns: new[] { "TenantId", "CounterpartyAccountId" });
        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_OperatingAccountId", table: "Invoices", columns: new[] { "TenantId", "OperatingAccountId" });
        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_TaxAccountId", table: "Invoices", columns: new[] { "TenantId", "TaxAccountId" });

        migrationBuilder.AddForeignKey(name: "FK_Invoices_Accounts_TenantId_CounterpartyAccountId", table: "Invoices", columns: new[] { "TenantId", "CounterpartyAccountId" }, principalTable: "Accounts", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Invoices_Accounts_TenantId_OperatingAccountId", table: "Invoices", columns: new[] { "TenantId", "OperatingAccountId" }, principalTable: "Accounts", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Invoices_Accounts_TenantId_TaxAccountId", table: "Invoices", columns: new[] { "TenantId", "TaxAccountId" }, principalTable: "Accounts", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Invoices_Accounts_TenantId_CounterpartyAccountId", table: "Invoices");
        migrationBuilder.DropForeignKey(name: "FK_Invoices_Accounts_TenantId_OperatingAccountId", table: "Invoices");
        migrationBuilder.DropForeignKey(name: "FK_Invoices_Accounts_TenantId_TaxAccountId", table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_TenantId_CounterpartyAccountId", table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_TenantId_OperatingAccountId", table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_TenantId_TaxAccountId", table: "Invoices");
        migrationBuilder.DropColumn(name: "CounterpartyAccountId", table: "Invoices");
        migrationBuilder.DropColumn(name: "OperatingAccountId", table: "Invoices");
        migrationBuilder.DropColumn(name: "TaxAccountId", table: "Invoices");
    }
}
