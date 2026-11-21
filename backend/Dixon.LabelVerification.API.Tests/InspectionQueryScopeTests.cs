using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;

namespace Dixon.LabelVerification.API.Tests;

public sealed class InspectionQueryScopeTests
{
    [Theory]
    [InlineData("Admin", true)]
    [InlineData("Supervisor", true)]
    [InlineData("Operator", true)]
    [InlineData("Viewer", false)]
    public void ApplicationRoles_AcceptsOnlyConfiguredRoles(string role, bool expected) =>
        Assert.Equal(expected, ApplicationRoles.IsValid(role));

    [Fact]
    public void Apply_EnforcesBrandAndRoleScopes()
    {
        var operatorId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        var otherBrandId = Guid.NewGuid();
        var inspections = new[]
        {
            new InspectionSession
            {
                InspectionNumber = "OPERATOR-1",
                CreatedByUserId = operatorId,
                BrandId = brandId
            },
            new InspectionSession
            {
                InspectionNumber = "OPERATOR-2",
                CreatedByUserId = Guid.NewGuid(),
                BrandId = brandId
            },
            new InspectionSession
            {
                InspectionNumber = "OTHER-BRAND",
                CreatedByUserId = operatorId,
                BrandId = otherBrandId
            }
        };

        var operatorResults = InspectionQueryScope.Apply(
            inspections.AsQueryable(), operatorId, ApplicationRoles.Operator, brandId).ToArray();
        var supervisorResults = InspectionQueryScope.Apply(
            inspections.AsQueryable(), operatorId, ApplicationRoles.Supervisor, brandId).ToArray();
        var adminResults = InspectionQueryScope.Apply(
            inspections.AsQueryable(), operatorId, ApplicationRoles.Admin, null).ToArray();

        Assert.Single(operatorResults);
        Assert.Equal("OPERATOR-1", operatorResults[0].InspectionNumber);
        Assert.Equal(2, supervisorResults.Length);
        Assert.Equal(3, adminResults.Length);
    }

    [Theory]
    [InlineData("Admin", null, true)]
    [InlineData("Supervisor", null, false)]
    [InlineData("Operator", null, false)]
    [InlineData("Supervisor", "a7a78686-b76a-44ba-9be7-48d3ef2478b1", true)]
    [InlineData("Operator", "a7a78686-b76a-44ba-9be7-48d3ef2478b1", true)]
    public void HasValidBrandAssignment_EnforcesRoleRequirements(
        string role, string? brandId, bool expected)
    {
        Guid? parsedBrandId = brandId is null ? null : Guid.Parse(brandId);
        Assert.Equal(expected, ApplicationRoles.HasValidBrandAssignment(role, parsedBrandId));
    }
}
