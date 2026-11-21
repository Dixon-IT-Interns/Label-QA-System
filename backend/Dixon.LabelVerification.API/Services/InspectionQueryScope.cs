using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class InspectionQueryScope
{
    public static IQueryable<InspectionSession> Apply(
        IQueryable<InspectionSession> inspections,
        Guid userId,
        string role,
        Guid? brandId)
    {
        if (role == ApplicationRoles.Admin)
            return inspections;
        if (!brandId.HasValue)
            throw new UnauthorizedAccessException("The authenticated account has no assigned brand.");
        if (role == ApplicationRoles.Supervisor)
            return inspections.Where(x => x.BrandId == brandId.Value);
        if (role == ApplicationRoles.Operator)
            return inspections.Where(x =>
                x.BrandId == brandId.Value && x.CreatedByUserId == userId);
        throw new UnauthorizedAccessException("The authenticated role is invalid.");
    }
}
