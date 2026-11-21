using System.Security.Claims;
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
public sealed class DashboardController(LabelVerificationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> Get(CancellationToken cancellationToken)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var userId))
            return Unauthorized();
        var isSupervisor = User.IsInRole(ApplicationRoles.Supervisor);
        var now = DateTimeOffset.UtcNow;
        var startOfDay = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, TimeSpan.Zero);
        var inspections = InspectionQueryScope.Apply(
            db.InspectionSessions.AsNoTracking(), userId, isSupervisor);
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
        var profiles = await db.LabelProfiles.CountAsync(x => x.IsActive, cancellationToken);
        return Ok(new DashboardResponse(
            total, today, passed, failed, review, profiles,
            total == 0 ? 0 : Math.Round((double)passed / total * 100, 1),
            recent));
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
    IReadOnlyList<RecentInspectionResponse> RecentInspections);
