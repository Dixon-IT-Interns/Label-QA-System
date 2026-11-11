namespace Dixon.LabelVerification.API.Entities;

public sealed class FieldDefinition : AuditedEntity
{
    public Guid LabelProfileId { get; set; }
    public required string FieldName { get; set; }
    public required string DisplayName { get; set; }
    public string DataType { get; set; } = "Text";
    public bool Required { get; set; } = true;
    public bool Critical { get; set; }
    public double OcrConfidenceThreshold { get; set; } = 0.9;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public FieldComparisonStrategy ComparisonStrategy { get; set; } = FieldComparisonStrategy.NormalizedText;
    public decimal NumericAbsoluteTolerance { get; set; }
    public decimal NumericRelativeTolerance { get; set; }
    public LabelProfile LabelProfile { get; set; } = null!;
    public ICollection<FieldAlias> Aliases { get; set; } = new List<FieldAlias>();
    public ICollection<FieldComparison> Comparisons { get; set; } = new List<FieldComparison>();
}
