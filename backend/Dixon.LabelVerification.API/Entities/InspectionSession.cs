namespace Dixon.LabelVerification.API.Entities;

public sealed class InspectionSession : AuditedEntity
{
    public Guid BrandId { get; set; }
    public Guid ModelId { get; set; }
    public Guid CreatedByUserId { get; set; }
    public Guid? LabelProfileCOId { get; set; }
    public Guid? LabelProfileConfigId { get; set; }
    public required string InspectionNumber { get; set; }
    public string? ProductionOrderNumber { get; set; }
    public string? WorkOrderNumber { get; set; }
    public InspectionStatus Status { get; set; } = InspectionStatus.READY;
    public InspectionStatus COStatus { get; set; } = InspectionStatus.READY;
    public InspectionStatus ConfigStatus { get; set; } = InspectionStatus.READY;
    public InspectionStatus SerialStatus { get; set; } = InspectionStatus.NOT_APPLICABLE;
    public string? SerialNumber { get; set; }
    public DateTimeOffset StartedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public long? ProcessingDurationMs { get; set; }
    public string? FinalRemarks { get; set; }
    public byte[] RowVersion { get; set; } = [];
    public Brand Brand { get; set; } = null!;
    public LaptopModel Model { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public LabelProfile? LabelProfileCO { get; set; }
    public LabelProfile? LabelProfileConfig { get; set; }
    public ICollection<InspectionImage> Images { get; set; } = new List<InspectionImage>();
    public ICollection<StageResult> StageResults { get; set; } = new List<StageResult>();
    public ICollection<InspectionLog> Logs { get; set; } = new List<InspectionLog>();
    public ICollection<InspectionReport> Reports { get; set; } = new List<InspectionReport>();
}
