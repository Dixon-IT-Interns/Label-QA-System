namespace Dixon.LabelVerification.API.Entities;

public sealed class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string SettingKey { get; set; }
    public string? SettingValue { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
