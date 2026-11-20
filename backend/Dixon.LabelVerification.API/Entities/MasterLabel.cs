namespace Dixon.LabelVerification.API.Entities;

public sealed class MasterLabel : AuditedEntity
{
    public Guid BrandId { get; set; }
    public Guid ModelId { get; set; }
    public Guid LabelProfileId { get; set; }
    public LabelType LabelType { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StoredFileName { get; set; }
    public required string StoragePath { get; set; }
    public required string Sha256Hash { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public required string Engine { get; set; }
    public required string EngineVersion { get; set; }
    public InspectionStatus OcrStatus { get; set; }
    public string? RawExtractedText { get; set; }
    public string? OcrDataJson { get; set; }
    public double? OverallConfidence { get; set; }
    public long? ProcessingDurationMs { get; set; }
    public string ImageQualityStatus { get; set; } = "ACCEPTED";
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public LaptopModel Model { get; set; } = null!;
    public LabelProfile LabelProfile { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
    public ICollection<MasterLabelField> Fields { get; set; } = new List<MasterLabelField>();
}
