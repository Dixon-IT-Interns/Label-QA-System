namespace Dixon.LabelVerification.API.Entities;

public sealed class User : AuditedEntity
{
    public required string FullName { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public UserRole UserRole { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public ICollection<InspectionSession> CreatedInspections { get; set; } = new List<InspectionSession>();
    public ICollection<InspectionLog> InspectionLogs { get; set; } = new List<InspectionLog>();
}
