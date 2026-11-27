using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GhoghnoosAcc.Data.Migrations;

public partial class AddInvoicesAndPayments : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Invoices",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                InvoiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PartyId = table.Column<int>(type: "integer", nullable: false),
                JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Subtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                TaxTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Invoices", x => x.Id);
                table.UniqueConstraint("AK_Invoices_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey("FK_Invoices_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Invoices_Parties_TenantId_PartyId", x => new { x.TenantId, x.PartyId }, "Parties", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_Invoices_JournalEntries_TenantId_JournalEntryId", x => new { x.TenantId, x.JournalEntryId }, "JournalEntries", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "InvoiceLines",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<int>(type: "integer", nullable: false),
                Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<decimal>(type: "numeric(18,3)", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                TaxRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                LineSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                LineTax = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                LineTotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_InvoiceLines", x => x.Id);
                table.ForeignKey("FK_InvoiceLines_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_InvoiceLines_Invoices_TenantId_InvoiceId", x => new { x.TenantId, x.InvoiceId }, "Invoices", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PaymentTransactions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false).Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Number = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PartyId = table.Column<int>(type: "integer", nullable: false),
                InvoiceId = table.Column<int>(type: "integer", nullable: true),
                CashAccountId = table.Column<int>(type: "integer", nullable: false),
                JournalEntryId = table.Column<int>(type: "integer", nullable: true),
                Amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                table.UniqueConstraint("AK_PaymentTransactions_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey("FK_PaymentTransactions_Tenants_TenantId", x => x.TenantId, "Tenants", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PaymentTransactions_Parties_TenantId_PartyId", x => new { x.TenantId, x.PartyId }, "Parties", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PaymentTransactions_Invoices_TenantId_InvoiceId", x => new { x.TenantId, x.InvoiceId }, "Invoices", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PaymentTransactions_Accounts_TenantId_CashAccountId", x => new { x.TenantId, x.CashAccountId }, "Accounts", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_PaymentTransactions_JournalEntries_TenantId_JournalEntryId", x => new { x.TenantId, x.JournalEntryId }, "JournalEntries", new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_Number", table: "Invoices", columns: new[] { "TenantId", "Number" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_PartyId", table: "Invoices", columns: new[] { "TenantId", "PartyId" });
        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_JournalEntryId", table: "Invoices", columns: new[] { "TenantId", "JournalEntryId" });
        migrationBuilder.CreateIndex(name: "IX_InvoiceLines_TenantId_InvoiceId", table: "InvoiceLines", columns: new[] { "TenantId", "InvoiceId" });
        migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_TenantId_Number", table: "PaymentTransactions", columns: new[] { "TenantId", "Number" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_TenantId_PartyId", table: "PaymentTransactions", columns: new[] { "TenantId", "PartyId" });
        migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_TenantId_InvoiceId", table: "PaymentTransactions", columns: new[] { "TenantId", "InvoiceId" });
        migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_TenantId_CashAccountId", table: "PaymentTransactions", columns: new[] { "TenantId", "CashAccountId" });
        migrationBuilder.CreateIndex(name: "IX_PaymentTransactions_TenantId_JournalEntryId", table: "PaymentTransactions", columns: new[] { "TenantId", "JournalEntryId" });
        migrationBuilder.CreateIndex(name: "IX_Invoices_TenantId_Status_InvoiceDate", table: "Invoices", columns: new[] { "TenantId", "Status", "InvoiceDate" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PaymentTransactions");
        migrationBuilder.DropTable(name: "InvoiceLines");
        migrationBuilder.DropTable(name: "Invoices");
    }
}
