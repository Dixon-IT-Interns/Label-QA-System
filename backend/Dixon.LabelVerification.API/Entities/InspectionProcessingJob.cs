namespace Dixon.LabelVerification.API.Entities;

public sealed class InspectionProcessingJob : AuditedEntity
{
    public Guid InspectionSessionId { get; set; }
    public string Status { get; set; } = "PENDING";
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public InspectionSession InspectionSession { get; set; } = null!;
}
