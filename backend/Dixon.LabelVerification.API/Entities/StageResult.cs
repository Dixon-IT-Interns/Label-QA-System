namespace Dixon.LabelVerification.API.Entities;

public sealed class StageResult : AuditedEntity
{
    public Guid InspectionSessionId { get; set; }
    public InspectionStageType StageType { get; set; }
    public LabelType LabelType { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.PROCESSING;
    public int TotalFields { get; set; }
    public int MatchedFields { get; set; }
    public int MismatchedFields { get; set; }
    public int UnreadableFields { get; set; }
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public string? Remarks { get; set; }
    public DateTimeOffset? EvaluatedAtUtc { get; set; }
    public InspectionSession InspectionSession { get; set; } = null!;
    public ICollection<FieldComparison> FieldComparisons { get; set; } = new List<FieldComparison>();
}
