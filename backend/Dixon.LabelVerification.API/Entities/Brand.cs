namespace Dixon.LabelVerification.API.Entities;

public sealed class Brand : AuditedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<LaptopModel> Models { get; set; } = new List<LaptopModel>();
    public ICollection<LabelProfile> LabelProfiles { get; set; } = new List<LabelProfile>();
}
