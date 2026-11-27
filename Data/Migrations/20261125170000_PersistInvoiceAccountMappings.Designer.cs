using GhoghnoosAcc.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GhoghnoosAcc.Data.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261125170000_PersistInvoiceAccountMappings")]
public partial class PersistInvoiceAccountMappings
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        // Model snapshot is maintained separately; migration adds nullable account mapping columns.
    }
}
