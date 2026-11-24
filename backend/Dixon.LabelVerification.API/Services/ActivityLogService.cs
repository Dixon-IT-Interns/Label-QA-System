using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class ActivityLogService(
    LabelVerificationDbContext db,
    AuthenticatedUserContext currentUser) : IActivityLogService
{
    public async Task RecordAsync(string action, string details, Guid? brandId = null, Guid? inspectionId = null,
        Guid? entityId = null, string? entityType = null, string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == currentUser.UserId, cancellationToken);
        var resolvedBrandId = brandId ?? currentUser.BrandId;
        var brandName = resolvedBrandId.HasValue
            ? await db.Brands.AsNoTracking().Where(x => x.Id == resolvedBrandId.Value).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        db.ActivityLogs.Add(new ActivityLog
        {
            UserAccountId = currentUser.UserId,
            UserName = user?.FullName ?? currentUser.UserId.ToString(),
            Role = currentUser.Role,
            BrandId = resolvedBrandId,
            BrandName = brandName,
            InspectionSessionId = inspectionId,
            EntityId = entityId,
            EntityType = entityType,
            Action = action,
            Details = details,
            IPAddress = ipAddress
        });
    }
}
