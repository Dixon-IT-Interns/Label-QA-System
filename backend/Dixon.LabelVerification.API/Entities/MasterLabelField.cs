namespace Dixon.LabelVerification.API.Entities;

public sealed class MasterLabelField : AuditedEntity
{
    public Guid MasterLabelId { get; set; }
    public required string FieldKey { get; set; }
    public required string DisplayName { get; set; }
    public string? ExtractedValue { get; set; }
    public string? NormalizedValue { get; set; }
    public double Confidence { get; set; }
    public string? BoundingBoxJson { get; set; }
    public required string Reason { get; set; }
    public MasterLabel MasterLabel { get; set; } = null!;
}
