using System.Security.Claims;
using GhoghnoosAcc.Services;
using Microsoft.AspNetCore.Http;

namespace GhoghnoosAcc.Tests;

public sealed class TenantContextTests
{
    [Fact]
    public void TenantId_IsNull_WhenNoHttpContextExists()
    {
        var tenantContext = new TenantContext(new HttpContextAccessor());

        Assert.Null(tenantContext.TenantId);
        Assert.False(tenantContext.HasTenant);
    }

    [Fact]
    public void TenantId_IsNull_WhenPrincipalIsUnauthenticated()
    {
        var tenantId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(TenantContext.TenantIdClaimType, tenantId.ToString())]));

        var tenantContext = CreateContext(principal);

        Assert.Null(tenantContext.TenantId);
        Assert.False(tenantContext.HasTenant);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void TenantId_IsNull_WhenClaimIsMissingOrInvalid(string? claimValue)
    {
        IEnumerable<Claim> claims = claimValue is null
            ? Array.Empty<Claim>()
            : new[] { new Claim(TenantContext.TenantIdClaimType, claimValue) };
        var principal = AuthenticatedPrincipal(claims);

        var tenantContext = CreateContext(principal);

        Assert.Null(tenantContext.TenantId);
        Assert.False(tenantContext.HasTenant);
    }

    [Fact]
    public void TenantId_UsesValidTenantClaimFromAuthenticatedPrincipal()
    {
        var expectedTenantId = Guid.NewGuid();
        var principal = AuthenticatedPrincipal(
            [new Claim(TenantContext.TenantIdClaimType, expectedTenantId.ToString())]);

        var tenantContext = CreateContext(principal);

        Assert.Equal(expectedTenantId, tenantContext.TenantId);
        Assert.True(tenantContext.HasTenant);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(IEnumerable<Claim> claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    private static TenantContext CreateContext(ClaimsPrincipal principal)
    {
        var accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };

        return new TenantContext(accessor);
    }
}
