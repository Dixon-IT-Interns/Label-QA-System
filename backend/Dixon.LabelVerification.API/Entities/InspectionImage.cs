namespace Dixon.LabelVerification.API.Entities;

public sealed class InspectionImage : AuditedEntity
{
    public Guid InspectionSessionId { get; set; }
    public InspectionImageType ImageType { get; set; }
    public LabelType LabelType { get; set; }
    public ImageRole Role { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StoredFileName { get; set; }
    public required string StoragePath { get; set; }
    public required string Sha256Hash { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string ImageQualityStatus { get; set; } = "PENDING";
    public InspectionSession InspectionSession { get; set; } = null!;
    public User UploadedByUser { get; set; } = null!;
    public ICollection<OcrResult> OcrResults { get; set; } = new List<OcrResult>();
}
