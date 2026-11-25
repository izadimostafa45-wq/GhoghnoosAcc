using System.Security.Claims;
using GhoghnoosAcc.Models;
using GhoghnoosAcc.Services;

namespace GhoghnoosAcc.Tests;

public sealed class TenantRoleAuthorizationTests
{
    [Theory]
    [InlineData(TenantRoles.Owner, true)]
    [InlineData(TenantRoles.Admin, true)]
    [InlineData(TenantRoles.Accountant, true)]
    [InlineData(TenantRoles.Sales, false)]
    [InlineData(TenantRoles.Service, false)]
    [InlineData(TenantRoles.ReadOnly, false)]
    public void WriteAccess_IsGrantedOnlyToAccountingAdministrators(string role, bool expected)
    {
        var principal = Principal(Guid.NewGuid(), role);

        Assert.Equal(expected, TenantRoleAuthorization.HasAnyRole(principal, TenantRoleAuthorization.WriteRoles));
    }

    [Theory]
    [InlineData(TenantRoles.Owner)]
    [InlineData(TenantRoles.Admin)]
    [InlineData(TenantRoles.Accountant)]
    [InlineData(TenantRoles.Sales)]
    [InlineData(TenantRoles.Service)]
    [InlineData(TenantRoles.ReadOnly)]
    public void ReadAccess_IsGrantedToKnownActiveMembershipRoles(string role)
    {
        var principal = Principal(Guid.NewGuid(), role);

        Assert.True(TenantRoleAuthorization.HasAnyRole(principal, TenantRoleAuthorization.ReadRoles));
    }

    [Theory]
    [InlineData("UnknownRole")]
    [InlineData("")]
    public void UnknownRole_IsRejected(string role)
    {
        var principal = Principal(Guid.NewGuid(), role);

        Assert.False(TenantRoleAuthorization.HasValidTenantAndRole(principal));
        Assert.False(TenantRoleAuthorization.HasAnyRole(principal, TenantRoleAuthorization.WriteRoles));
    }

    [Fact]
    public void MissingOrInvalidTenantClaim_IsRejectedEvenWithAdminRole()
    {
        var principal = Principal(Guid.Empty, TenantRoles.Admin);

        Assert.False(TenantRoleAuthorization.HasValidTenantAndRole(principal));
        Assert.False(TenantRoleAuthorization.HasAnyRole(principal, TenantRoleAuthorization.WriteRoles));
    }

    private static ClaimsPrincipal Principal(Guid tenantId, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(TenantContext.TenantIdClaimType, tenantId.ToString("D")),
            new Claim(TenantRoleAuthorization.RoleClaimType, role)
        ], authenticationType: "test"));
}
