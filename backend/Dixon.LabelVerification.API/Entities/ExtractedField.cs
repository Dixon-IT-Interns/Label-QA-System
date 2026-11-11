namespace Dixon.LabelVerification.API.Entities;

public sealed class ExtractedField : AuditedEntity
{
    public Guid OcrResultId { get; set; }
    public Guid? FieldDefinitionId { get; set; }
    public required string FieldKey { get; set; }
    public string? ExtractedValue { get; set; }
    public string? NormalizedValue { get; set; }
    public double Confidence { get; set; }
    public string? BoundingBoxJson { get; set; }
    public bool IsManuallyCorrected { get; set; }
    public Guid? CorrectedByUserId { get; set; }
    public string? CorrectionReason { get; set; }
    public OcrResult OcrResult { get; set; } = null!;
    public FieldDefinition? FieldDefinition { get; set; }
    public User? CorrectedByUser { get; set; }
}
