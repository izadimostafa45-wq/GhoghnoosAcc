using GhoghnoosAcc.Models;
using GhoghnoosAcc.Services;

namespace GhoghnoosAcc.Tests;

public sealed class TenantQueryExtensionsTests
{
    [Fact]
    public void ForTenant_ReturnsOnlyRecordsOwnedBySelectedTenant()
    {
        var selectedTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var accounts = new[]
        {
            new Account { Id = 1, TenantId = selectedTenant, Code = "100", Title = "Selected cash" },
            new Account { Id = 2, TenantId = otherTenant, Code = "100", Title = "Other cash" },
            new Account { Id = 3, TenantId = selectedTenant, Code = "200", Title = "Selected sales" }
        };

        var result = accounts.AsQueryable().ForTenant(selectedTenant).ToList();

        Assert.Equal(2, result.Count);
        Assert.All(result, account => Assert.Equal(selectedTenant, account.TenantId));
        Assert.DoesNotContain(result, account => account.TenantId == otherTenant);
    }

    [Fact]
    public void ForTenant_ReturnsNoRecordsWhenTenantHasNoData()
    {
        var selectedTenant = Guid.NewGuid();
        var entries = new[]
        {
            new JournalEntry { Id = 1, TenantId = Guid.NewGuid(), DocumentNumber = "JV-1" }
        };

        Assert.Empty(entries.AsQueryable().ForTenant(selectedTenant));
    }
}
