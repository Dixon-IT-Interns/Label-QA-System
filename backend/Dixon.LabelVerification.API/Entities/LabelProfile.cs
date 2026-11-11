namespace Dixon.LabelVerification.API.Entities;

public sealed class LabelProfile : AuditedEntity
{
    public Guid BrandId { get; set; }
    public Guid? ModelId { get; set; }
    public LabelType LabelType { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset EffectiveFromUtc { get; set; } = DateTimeOffset.UtcNow;
    public Brand Brand { get; set; } = null!;
    public LaptopModel? Model { get; set; }
    public ICollection<FieldDefinition> FieldDefinitions { get; set; } = new List<FieldDefinition>();
}
