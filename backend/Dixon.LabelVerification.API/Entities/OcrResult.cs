namespace Dixon.LabelVerification.API.Entities;

public sealed class OcrResult : AuditedEntity
{
    public Guid InspectionImageId { get; set; }
    public Guid InspectionSessionId { get; set; }
    public required string Engine { get; set; }
    public required string EngineVersion { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.PROCESSING;
    public string? RawExtractedText { get; set; }
    public string? ProcessedImagePath { get; set; }
    public long? ProcessingDurationMs { get; set; }
    public double? OverallConfidence { get; set; }
    public string? ExtractedFieldsJson { get; set; }
    public InspectionImage InspectionImage { get; set; } = null!;
    public InspectionSession InspectionSession { get; set; } = null!;
    public ICollection<ExtractedField> ExtractedFields { get; set; } = new List<ExtractedField>();
}
