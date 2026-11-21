using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionAccessService(LabelVerificationDbContext db) : IInspectionAccessService
{
    public async Task EnsureCanAccessAsync(
        Guid inspectionId,
        Guid userId,
        string role,
        Guid? brandId,
        CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken);
        if (inspection is null)
            throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        if (role != ApplicationRoles.Admin && inspection.BrandId != brandId)
            throw new ForbiddenAccessException("You do not have access to this brand's inspections.");
        if (role == ApplicationRoles.Operator && inspection.CreatedByUserId != userId)
            throw new ForbiddenAccessException("Operators can access only their own inspections.");
    }
}
