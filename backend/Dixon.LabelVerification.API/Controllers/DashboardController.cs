using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public sealed class DashboardController(
        LabelVerificationDbContext db,
    AuthenticatedUserContext currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var inspections = InspectionQueryScope.Apply(
            db.InspectionSessions.AsNoTracking(), currentUser.UserId, currentUser.Role, currentUser.BrandId);
        var today = await inspections.CountAsync(x => x.CreatedAtUtc >= startOfDay, cancellationToken);
        var total = await inspections.CountAsync(cancellationToken);
        var passed = await inspections.CountAsync(x => x.Status == InspectionStatus.PASS, cancellationToken);
        var failed = await inspections.CountAsync(x => x.Status == InspectionStatus.FAIL, cancellationToken);
        var review = await inspections.CountAsync(x => x.Status == InspectionStatus.REVIEW_REQUIRED, cancellationToken);
        var recent = await inspections.Include(x => x.Brand).Include(x => x.Model)
            .OrderByDescending(x => x.CreatedAtUtc).Take(8)
            .Select(x => new RecentInspectionResponse(
                x.Id, x.InspectionNumber, x.Brand.Name, x.Model.Code, x.Model.Name,
                x.Status, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        var profilesQuery = db.LabelProfiles.AsQueryable();
        if (!currentUser.IsAdmin)
            profilesQuery = profilesQuery.Where(x => x.BrandId == currentUser.BrandId);
        var profiles = await profilesQuery.CountAsync(x => x.IsActive, cancellationToken);
        var activities = db.ActivityLogs.AsNoTracking();
        if (currentUser.IsSupervisor)
            activities = activities.Where(x => x.BrandId == currentUser.BrandId);
        else if (currentUser.IsOperator)
            activities = activities.Where(x => x.UserAccountId == currentUser.UserId);
        var recentActivities = await activities.OrderByDescending(x => x.CreatedAtUtc).Take(30)
            .Select(x => new ActivityLogResponse(
                x.Id, x.CreatedAtUtc, x.UserName, x.Role, x.Action, x.Details,
                x.BrandName, x.InspectionSessionId, x.InspectionSession == null ? null : x.InspectionSession.InspectionNumber))
            .ToListAsync(cancellationToken);
        return Ok(new DashboardResponse(
            total, today, passed, failed, review, profiles,
            total == 0 ? 0 : Math.Round((double)passed / total * 100, 1),
            recent, recentActivities));
    }
}

public sealed record RecentInspectionResponse(
    Guid Id, string InspectionNumber, string BrandName, string ModelNumber,
    string ModelName, InspectionStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record DashboardResponse(
    int TotalInspections,
    int InspectionsToday,
    int PassCount,
    int FailCount,
    int ReviewRequiredCount,
    int ActiveProfiles,
    double PassRatePercent,
    IReadOnlyList<RecentInspectionResponse> RecentInspections,
    IReadOnlyList<ActivityLogResponse> Activities);

public sealed record ActivityLogResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string UserName,
    string Role,
    string Action,
    string Details,
    string? BrandName,
    Guid? InspectionId,
    string? InspectionNumber);
