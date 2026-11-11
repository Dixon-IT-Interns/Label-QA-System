namespace Dixon.LabelVerification.API.Entities;

public sealed class InspectionReport : AuditedEntity
{
    public Guid InspectionSessionId { get; set; }
    public required string ReportNumber { get; set; }
    public required string ReportFilePath { get; set; }
    public long FileSizeBytes { get; set; }
    public required string Sha256Hash { get; set; }
    public string ContentType { get; set; } = "application/pdf";
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public Guid GeneratedByUserId { get; set; }
    public InspectionSession InspectionSession { get; set; } = null!;
    public User GeneratedByUser { get; set; } = null!;
}
