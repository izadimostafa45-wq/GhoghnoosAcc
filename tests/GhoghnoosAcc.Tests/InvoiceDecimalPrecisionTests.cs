using GhoghnoosAcc.Services;

namespace GhoghnoosAcc.Tests;

public sealed class InvoiceDecimalPrecisionTests
{
    [Theory]
    [InlineData("12.123")]
    [InlineData("0.001")]
    public void Quantity_AllowsAtMostThreeDecimalPlaces(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(InvoiceDecimalPrecision.HasScaleAbove(value, 3));
    }

    [Fact]
    public void Quantity_RejectsMoreThanThreeDecimalPlaces()
    {
        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(12.1234m, 3));
    }

    [Theory]
    [InlineData("12.12")]
    [InlineData("0.01")]
    [InlineData("0")]
    public void MonetaryAndTaxValues_AllowAtMostTwoDecimalPlaces(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);
        Assert.False(InvoiceDecimalPrecision.HasScaleAbove(value, 2));
    }

    [Fact]
    public void MonetaryAndTaxValues_RejectMoreThanTwoDecimalPlaces()
    {
        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(12.123m, 2));
    }

    [Fact]
    public void ScaleCheck_DoesNotRoundOrModifyValue()
    {
        const decimal value = 12.1234m;

        Assert.True(InvoiceDecimalPrecision.HasScaleAbove(value, 2));
        Assert.Equal(12.1234m, value);
    }

    [Fact]
    public void Numeric18_2_AcceptsMaximumPositiveValue()
    {
        Assert.True(InvoiceDecimalPrecision.FitsNumeric18_2(9_999_999_999_999_999.99m));
    }

    [Fact]
    public void Numeric18_2_AcceptsMaximumNegativeValue()
    {
        Assert.True(InvoiceDecimalPrecision.FitsNumeric18_2(-9_999_999_999_999_999.99m));
    }

    [Theory]
    [InlineData("10000000000000000.00")]
    [InlineData("-10000000000000000.00")]
    public void Numeric18_2_RejectsValuesOutsideDatabaseRange(string rawValue)
    {
        var value = decimal.Parse(rawValue, System.Globalization.CultureInfo.InvariantCulture);

        Assert.False(InvoiceDecimalPrecision.FitsNumeric18_2(value));
    }
}


public sealed class TenantRoleAuthorizationTests
{
    [Fact]
    public void HasValidTenantAndRole_AcceptsKnownRoleAndNonEmptyTenant()
    {
        var principal = CreatePrincipal(Guid.NewGuid(), GhoghnoosAcc.Models.TenantRoles.Accountant);
        Assert.True(GhoghnoosAcc.Services.TenantRoleAuthorization.HasValidTenantAndRole(principal));
    }

    [Fact]
    public void HasValidTenantAndRole_RejectsMissingOrEmptyTenant()
    {
        Assert.False(GhoghnoosAcc.Services.TenantRoleAuthorization.HasValidTenantAndRole(
            CreatePrincipal(null, GhoghnoosAcc.Models.TenantRoles.Accountant)));
        Assert.False(GhoghnoosAcc.Services.TenantRoleAuthorization.HasValidTenantAndRole(
            CreatePrincipal(Guid.Empty, GhoghnoosAcc.Models.TenantRoles.Accountant)));
    }

    [Fact]
    public void HasValidTenantAndRole_RejectsUnknownRole()
    {
        Assert.False(GhoghnoosAcc.Services.TenantRoleAuthorization.HasValidTenantAndRole(
            CreatePrincipal(Guid.NewGuid(), "SuperUser")));
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Admin", true)]
    [InlineData("Accountant", true)]
    [InlineData("Sales", false)]
    [InlineData("Service", false)]
    [InlineData("ReadOnly", false)]
    public void WriteAuthorization_OnlyAllowsWriteRoles(string role, bool expected)
    {
        var principal = CreatePrincipal(Guid.NewGuid(), role);
        Assert.Equal(expected, GhoghnoosAcc.Services.TenantRoleAuthorization.HasAnyRole(
            principal, GhoghnoosAcc.Services.TenantRoleAuthorization.WriteRoles));
    }

    [Fact]
    public void MatchingWriteRoleWithoutTenant_IsNotAuthorized()
    {
        var principal = CreatePrincipal(null, GhoghnoosAcc.Models.TenantRoles.Owner);
        Assert.False(GhoghnoosAcc.Services.TenantRoleAuthorization.HasAnyRole(
            principal, GhoghnoosAcc.Services.TenantRoleAuthorization.WriteRoles));
    }

    private static System.Security.Claims.ClaimsPrincipal CreatePrincipal(Guid? tenantId, string? role)
    {
        var claims = new List<System.Security.Claims.Claim>();
        if (tenantId.HasValue)
            claims.Add(new System.Security.Claims.Claim(
                GhoghnoosAcc.Services.TenantContext.TenantIdClaimType, tenantId.Value.ToString()));
        if (role is not null)
            claims.Add(new System.Security.Claims.Claim(
                GhoghnoosAcc.Services.TenantRoleAuthorization.RoleClaimType, role));

        return new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(claims, authenticationType: "UnitTest"));
    }
}
