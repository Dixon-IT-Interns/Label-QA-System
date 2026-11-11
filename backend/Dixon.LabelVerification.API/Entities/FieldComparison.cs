namespace Dixon.LabelVerification.API.Entities;

public sealed class FieldComparison : AuditedEntity
{
    public Guid StageResultId { get; set; }
    public Guid? FieldDefinitionId { get; set; }
    public required string FieldKey { get; set; }
    public string? MasterValue { get; set; }
    public string? ActualValue { get; set; }
    public string? NormalizedMasterValue { get; set; }
    public string? NormalizedActualValue { get; set; }
    public FieldComparisonStrategy ComparisonStrategy { get; set; }
    public double? MasterConfidence { get; set; }
    public double? ActualConfidence { get; set; }
    public string? MismatchReason { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.PROCESSING;
    public StageResult StageResult { get; set; } = null!;
    public FieldDefinition? FieldDefinition { get; set; }
}
