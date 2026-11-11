namespace Dixon.LabelVerification.API.Entities;

public sealed class InspectionLog : AuditedEntity
{
    public Guid InspectionSessionId { get; set; }
    public Guid? UserAccountId { get; set; }
    public required string EventType { get; set; }
    public required string Details { get; set; }
    public string? PreviousValue { get; set; }
    public string? NewValue { get; set; }
    public string? IPAddress { get; set; }
    public InspectionSession InspectionSession { get; set; } = null!;
    public User? UserAccount { get; set; }
}
