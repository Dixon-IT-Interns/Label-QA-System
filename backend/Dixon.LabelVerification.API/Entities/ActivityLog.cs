namespace Dixon.LabelVerification.API.Entities;

public sealed class ActivityLog : AuditedEntity
{
    public Guid? UserAccountId { get; set; }
    public required string UserName { get; set; }
    public required string Role { get; set; }
    public Guid? BrandId { get; set; }
    public string? BrandName { get; set; }
    public Guid? InspectionSessionId { get; set; }
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public required string Action { get; set; }
    public required string Details { get; set; }
    public string? IPAddress { get; set; }
    public User? UserAccount { get; set; }
    public Brand? Brand { get; set; }
    public InspectionSession? InspectionSession { get; set; }
}
