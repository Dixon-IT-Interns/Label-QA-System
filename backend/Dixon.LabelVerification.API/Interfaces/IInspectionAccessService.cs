namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionAccessService
{
    Task EnsureCanAccessAsync(
        Guid inspectionId,
        Guid userId,
        bool isSupervisor,
        CancellationToken cancellationToken);
}
