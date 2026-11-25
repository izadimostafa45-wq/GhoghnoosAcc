using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhoghnoosAcc.Data.Migrations;

public partial class AddTenantOwnershipToAccounting : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(name: "TenantId", table: "Accounts", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TenantId", table: "Parties", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TenantId", table: "JournalEntries", type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(name: "TenantId", table: "JournalEntryLines", type: "uuid", nullable: true);

        // Existing accounting rows can only be assigned automatically when there is exactly
        // one tenant. Stop safely rather than guessing ownership in a multi-tenant database.
        migrationBuilder.Sql("""
            DO $$
            DECLARE tenant_count integer;
            DECLARE selected_tenant uuid;
            DECLARE has_accounting_data boolean;
            BEGIN
                SELECT COUNT(*) INTO tenant_count FROM "Tenants";
                SELECT
                    EXISTS (SELECT 1 FROM "Accounts") OR
                    EXISTS (SELECT 1 FROM "Parties") OR
                    EXISTS (SELECT 1 FROM "JournalEntries") OR
                    EXISTS (SELECT 1 FROM "JournalEntryLines")
                INTO has_accounting_data;

                IF has_accounting_data AND tenant_count <> 1 THEN
                    RAISE EXCEPTION 'Cannot migrate accounting ownership safely: existing accounting data requires exactly one tenant. Assign each record to the correct tenant before retrying.';
                END IF;

                IF tenant_count = 1 THEN
                    SELECT "Id" INTO selected_tenant FROM "Tenants" LIMIT 1;
                    UPDATE "Accounts" SET "TenantId" = selected_tenant;
                    UPDATE "Parties" SET "TenantId" = selected_tenant;
                    UPDATE "JournalEntries" SET "TenantId" = selected_tenant;
                    UPDATE "JournalEntryLines" SET "TenantId" = selected_tenant;
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(name: "TenantId", table: "Accounts", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "TenantId", table: "Parties", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "TenantId", table: "JournalEntries", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(name: "TenantId", table: "JournalEntryLines", type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

        migrationBuilder.DropForeignKey(name: "FK_Accounts_Accounts_ParentAccountId", table: "Accounts");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_Accounts_AccountId", table: "JournalEntryLines");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_JournalEntries_JournalEntryId", table: "JournalEntryLines");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_Parties_PartyId", table: "JournalEntryLines");
        migrationBuilder.DropIndex(name: "IX_Accounts_Code", table: "Accounts");
        migrationBuilder.DropIndex(name: "IX_JournalEntries_DocumentNumber", table: "JournalEntries");

        migrationBuilder.AddUniqueConstraint(name: "AK_Accounts_TenantId_Id", table: "Accounts", columns: new[] { "TenantId", "Id" });
        migrationBuilder.AddUniqueConstraint(name: "AK_Parties_TenantId_Id", table: "Parties", columns: new[] { "TenantId", "Id" });
        migrationBuilder.AddUniqueConstraint(name: "AK_JournalEntries_TenantId_Id", table: "JournalEntries", columns: new[] { "TenantId", "Id" });

        migrationBuilder.AddForeignKey(name: "FK_Accounts_Tenants_TenantId", table: "Accounts", column: "TenantId", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_Parties_Tenants_TenantId", table: "Parties", column: "TenantId", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntries_Tenants_TenantId", table: "JournalEntries", column: "TenantId", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_Tenants_TenantId", table: "JournalEntryLines", column: "TenantId", principalTable: "Tenants", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(name: "FK_Accounts_Accounts_TenantId_ParentAccountId", table: "Accounts", columns: new[] { "TenantId", "ParentAccountId" }, principalTable: "Accounts", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_Accounts_TenantId_AccountId", table: "JournalEntryLines", columns: new[] { "TenantId", "AccountId" }, principalTable: "Accounts", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_JournalEntries_TenantId_JournalEntryId", table: "JournalEntryLines", columns: new[] { "TenantId", "JournalEntryId" }, principalTable: "JournalEntries", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_Parties_TenantId_PartyId", table: "JournalEntryLines", columns: new[] { "TenantId", "PartyId" }, principalTable: "Parties", principalColumns: new[] { "TenantId", "Id" }, onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(name: "IX_Accounts_TenantId_Code", table: "Accounts", columns: new[] { "TenantId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_Accounts_TenantId_ParentAccountId", table: "Accounts", columns: new[] { "TenantId", "ParentAccountId" });
        migrationBuilder.CreateIndex(name: "IX_JournalEntries_TenantId_DocumentNumber", table: "JournalEntries", columns: new[] { "TenantId", "DocumentNumber" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_JournalEntryLines_TenantId_AccountId", table: "JournalEntryLines", columns: new[] { "TenantId", "AccountId" });
        migrationBuilder.CreateIndex(name: "IX_JournalEntryLines_TenantId_JournalEntryId", table: "JournalEntryLines", columns: new[] { "TenantId", "JournalEntryId" });
        migrationBuilder.CreateIndex(name: "IX_JournalEntryLines_TenantId_PartyId", table: "JournalEntryLines", columns: new[] { "TenantId", "PartyId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_Accounts_Accounts_TenantId_ParentAccountId", table: "Accounts");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_Accounts_TenantId_AccountId", table: "JournalEntryLines");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_JournalEntries_TenantId_JournalEntryId", table: "JournalEntryLines");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_Parties_TenantId_PartyId", table: "JournalEntryLines");
        migrationBuilder.DropForeignKey(name: "FK_Accounts_Tenants_TenantId", table: "Accounts");
        migrationBuilder.DropForeignKey(name: "FK_Parties_Tenants_TenantId", table: "Parties");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntries_Tenants_TenantId", table: "JournalEntries");
        migrationBuilder.DropForeignKey(name: "FK_JournalEntryLines_Tenants_TenantId", table: "JournalEntryLines");

        migrationBuilder.DropUniqueConstraint(name: "AK_Accounts_TenantId_Id", table: "Accounts");
        migrationBuilder.DropUniqueConstraint(name: "AK_Parties_TenantId_Id", table: "Parties");
        migrationBuilder.DropUniqueConstraint(name: "AK_JournalEntries_TenantId_Id", table: "JournalEntries");

        migrationBuilder.DropIndex(name: "IX_Accounts_TenantId_Code", table: "Accounts");
        migrationBuilder.DropIndex(name: "IX_Accounts_TenantId_ParentAccountId", table: "Accounts");
        migrationBuilder.DropIndex(name: "IX_JournalEntries_TenantId_DocumentNumber", table: "JournalEntries");
        migrationBuilder.DropIndex(name: "IX_JournalEntryLines_TenantId_AccountId", table: "JournalEntryLines");
        migrationBuilder.DropIndex(name: "IX_JournalEntryLines_TenantId_JournalEntryId", table: "JournalEntryLines");
        migrationBuilder.DropIndex(name: "IX_JournalEntryLines_TenantId_PartyId", table: "JournalEntryLines");

        migrationBuilder.AddForeignKey(name: "FK_Accounts_Accounts_ParentAccountId", table: "Accounts", column: "ParentAccountId", principalTable: "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_Accounts_AccountId", table: "JournalEntryLines", column: "AccountId", principalTable: "Accounts", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_JournalEntries_JournalEntryId", table: "JournalEntryLines", column: "JournalEntryId", principalTable: "JournalEntries", principalColumn: "Id", onDelete: ReferentialAction.Cascade);
        migrationBuilder.AddForeignKey(name: "FK_JournalEntryLines_Parties_PartyId", table: "JournalEntryLines", column: "PartyId", principalTable: "Parties", principalColumn: "Id", onDelete: ReferentialAction.Restrict);

        migrationBuilder.CreateIndex(name: "IX_Accounts_Code", table: "Accounts", column: "Code", unique: true);
        migrationBuilder.CreateIndex(name: "IX_JournalEntries_DocumentNumber", table: "JournalEntries", column: "DocumentNumber", unique: true);
        migrationBuilder.DropColumn(name: "TenantId", table: "JournalEntryLines");
        migrationBuilder.DropColumn(name: "TenantId", table: "JournalEntries");
        migrationBuilder.DropColumn(name: "TenantId", table: "Parties");
        migrationBuilder.DropColumn(name: "TenantId", table: "Accounts");
    }
}
