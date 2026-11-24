namespace Dixon.LabelVerification.API.Interfaces;

public interface IActivityLogService
{
    Task RecordAsync(string action, string details, Guid? brandId = null, Guid? inspectionId = null,
        Guid? entityId = null, string? entityType = null, string? ipAddress = null,
        CancellationToken cancellationToken = default);
}
