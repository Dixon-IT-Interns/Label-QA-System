using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class InspectionQueryScope
{
    public static IQueryable<InspectionSession> Apply(
        IQueryable<InspectionSession> inspections,
        Guid userId,
        bool isSupervisor) =>
        isSupervisor
            ? inspections
            : inspections.Where(x => x.CreatedByUserId == userId);
}
