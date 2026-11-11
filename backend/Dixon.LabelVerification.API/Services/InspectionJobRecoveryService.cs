using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionJobRecoveryService(
    IServiceScopeFactory scopeFactory,
    ILogger<InspectionJobRecoveryService> logger) : BackgroundService
{
    private static readonly TimeSpan StaleAfter = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await RecoverStaleJobsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inspection job recovery check failed.");
            }
        }
    }

    private async Task RecoverStaleJobsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LabelVerificationDbContext>();
        var cutoff = DateTimeOffset.UtcNow - StaleAfter;
        var staleJobs = await db.InspectionProcessingJobs
            .Where(x => x.Status == "PROCESSING" && x.StartedAtUtc < cutoff)
            .ToListAsync(cancellationToken);

        foreach (var job in staleJobs)
        {
            var inspection = await db.InspectionSessions
                .SingleOrDefaultAsync(x => x.Id == job.InspectionSessionId, cancellationToken);
            if (inspection is null || inspection.Status != InspectionStatus.PROCESSING) continue;

            job.Status = "FAILED";
            job.LastError = "Processing was interrupted and marked recoverable by the background recovery check.";
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
            inspection.Status = InspectionStatus.ERROR;
            inspection.CompletedAtUtc = null;
            db.InspectionLogs.Add(new InspectionLog
            {
                InspectionSessionId = inspection.Id,
                EventType = "PROCESSING_RECOVERED",
                Details = "Stale processing job was marked ERROR. Validate the inspection before retrying."
            });
        }

        if (staleJobs.Count > 0) await db.SaveChangesAsync(cancellationToken);
    }
}
