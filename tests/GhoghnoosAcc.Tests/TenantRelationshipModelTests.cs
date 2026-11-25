using GhoghnoosAcc.Data;
using GhoghnoosAcc.Models;
using Microsoft.EntityFrameworkCore;

namespace GhoghnoosAcc.Tests;

public sealed class TenantRelationshipModelTests
{
    [Fact]
    public void AccountingRelationships_IncludeTenantIdInForeignKeys()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=model_only;Password=model_only")
            .Options;

        using var db = new AppDbContext(options);

        AssertTenantAwareForeignKey<Account>(db, nameof(Account.ParentAccountId), nameof(Account.TenantId));
        AssertTenantAwareForeignKey<JournalEntryLine>(db, nameof(JournalEntryLine.AccountId), nameof(JournalEntryLine.TenantId));
        AssertTenantAwareForeignKey<JournalEntryLine>(db, nameof(JournalEntryLine.PartyId), nameof(JournalEntryLine.TenantId));
        AssertTenantAwareForeignKey<JournalEntryLine>(db, nameof(JournalEntryLine.JournalEntryId), nameof(JournalEntryLine.TenantId));
    }

    private static void AssertTenantAwareForeignKey<TEntity>(
        AppDbContext db,
        string relatedIdProperty,
        string tenantIdProperty)
        where TEntity : class
    {
        var entityType = db.Model.FindEntityType(typeof(TEntity));
        Assert.NotNull(entityType);

        var foreignKey = entityType!.GetForeignKeys()
            .SingleOrDefault(fk => fk.Properties.Any(property => property.Name == relatedIdProperty));

        Assert.NotNull(foreignKey);
        Assert.Contains(foreignKey!.Properties, property => property.Name == tenantIdProperty);
        Assert.Contains(foreignKey.PrincipalKey.Properties, property => property.Name == tenantIdProperty);
    }
}
