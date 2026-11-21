using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionManagementService
{
    Task<InspectionDetailsResponse> CreateAsync(
        CreateInspectionRequest request,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedInspectionResponse> GetAllAsync(
        InspectionListQuery query,
        Guid userId,
        bool isSupervisor,
        CancellationToken cancellationToken);
    Task<InspectionDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<InspectionDetailsResponse> UpdateAsync(
        Guid id,
        CreateInspectionRequest request,
        Guid operatorUserId,
        string? ipAddress,
        CancellationToken cancellationToken);
    Task<InspectionImageResponse> UploadImageAsync(
        Guid inspectionId,
        InspectionImageType imageType,
        IFormFile file,
        Guid operatorUserId,
        string? ipAddress,
        string requestId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InspectionImageResponse>> GetImagesAsync(Guid inspectionId, CancellationToken cancellationToken);
    Task<InspectionImageContent> GetImageAsync(Guid inspectionId, Guid imageId, CancellationToken cancellationToken);
    Task<InspectionDetailsResponse> CorrectFieldAsync(
        Guid inspectionId, Guid imageId, Guid fieldDefinitionId, ManualFieldCorrectionRequest request,
        Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken);
    Task DeleteImageAsync(Guid inspectionId, Guid imageId, Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<InspectionStatus> ValidateAsync(Guid inspectionId, Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<InspectionStatus> ProcessAsync(Guid inspectionId, Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken);
    Task CancelAsync(Guid inspectionId, Guid operatorUserId, string? ipAddress, CancellationToken cancellationToken);
    Task<IReadOnlyList<InspectionLogResponse>> GetLogsAsync(Guid inspectionId, CancellationToken cancellationToken);
}
