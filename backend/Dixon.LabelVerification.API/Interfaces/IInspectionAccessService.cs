namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionAccessService
{
    Task EnsureCanAccessAsync(
        Guid inspectionId,
        Guid userId,
        string role,
        Guid? brandId,
        CancellationToken cancellationToken);
}
