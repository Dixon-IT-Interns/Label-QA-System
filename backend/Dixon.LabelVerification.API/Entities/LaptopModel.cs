namespace Dixon.LabelVerification.API.Entities;

public sealed class LaptopModel : AuditedEntity
{
    public Guid BrandId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Brand Brand { get; set; } = null!;
    public ICollection<LabelProfile> LabelProfiles { get; set; } = new List<LabelProfile>();
    public ICollection<InspectionSession> InspectionSessions { get; set; } = new List<InspectionSession>();
}
