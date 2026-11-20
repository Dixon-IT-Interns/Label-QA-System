using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionAccessService(LabelVerificationDbContext db) : IInspectionAccessService
{
    public async Task EnsureCanAccessAsync(
        Guid inspectionId,
        Guid userId,
        bool isSupervisor,
        CancellationToken cancellationToken)
    {
        var canAccess = await InspectionQueryScope.Apply(
                db.InspectionSessions.AsNoTracking(), userId, isSupervisor)
            .AnyAsync(x => x.Id == inspectionId, cancellationToken);
        if (!canAccess)
            throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
    }
}
