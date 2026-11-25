using GhoghnoosAcc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhoghnoosAcc.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261125140000_AddInvoicesAndPayments")]
public partial class AddInvoicesAndPayments
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // The live model snapshot remains the source of truth for future migrations.
        // This migration only adds the invoice and payment tables.
    }
}
