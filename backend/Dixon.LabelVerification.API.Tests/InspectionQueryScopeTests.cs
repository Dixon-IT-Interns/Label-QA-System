using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;

namespace Dixon.LabelVerification.API.Tests;

public sealed class InspectionQueryScopeTests
{
    [Theory]
    [InlineData("Supervisor", true)]
    [InlineData("Operator", true)]
    [InlineData("Admin", false)]
    [InlineData("Viewer", false)]
    public void ApplicationRoles_AcceptsOnlySupervisorAndOperator(string role, bool expected) =>
        Assert.Equal(expected, ApplicationRoles.IsValid(role));

    [Fact]
    public void Apply_FiltersOperatorsToTheirOwnInspections_AndLeavesSupervisorScopeUnfiltered()
    {
        var operatorId = Guid.NewGuid();
        var inspections = new[]
        {
            new InspectionSession
            {
                InspectionNumber = "OPERATOR-1",
                CreatedByUserId = operatorId
            },
            new InspectionSession
            {
                InspectionNumber = "OPERATOR-2",
                CreatedByUserId = Guid.NewGuid()
            }
        };

        var operatorResults = InspectionQueryScope.Apply(inspections.AsQueryable(), operatorId, false).ToArray();
        var supervisorResults = InspectionQueryScope.Apply(inspections.AsQueryable(), operatorId, true).ToArray();

        Assert.Single(operatorResults);
        Assert.Equal("OPERATOR-1", operatorResults[0].InspectionNumber);
        Assert.Equal(2, supervisorResults.Length);
    }
}
