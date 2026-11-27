using GhoghnoosAcc.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhoghnoosAcc.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261125120000_AddTenantOwnership")]
public sealed class AddTenantOwnership : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Tenant tables are already created by InitialSchema. The accounting TenantId
        // columns and tenant-aware constraints are handled by the following
        // AddTenantOwnershipToAccounting migration. This historical migration used
        // to duplicate those changes and caused PostgreSQL duplicate-column errors.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty: this migration is retained to preserve migration history.
    }
}
