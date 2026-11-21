using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IMasterLabelService
{
    Task<IReadOnlyList<MasterLabelResponse>> GetAllAsync(
        Guid? brandId, Guid? modelId, CancellationToken cancellationToken);

    Task<IReadOnlyList<MasterLabelResponse>> GetByModelAsync(Guid modelId, CancellationToken cancellationToken);

    Task<MasterLabelAvailabilityResponse> GetAvailabilityAsync(Guid modelId, CancellationToken cancellationToken);

    Task<MasterLabelPairResponse> SavePairAsync(
        SaveMasterLabelPairRequest request, Guid userId, CancellationToken cancellationToken);

    Task<DetectedModelResponse> DetectModelAsync(
        DetectModelRequest request, CancellationToken cancellationToken);

    Task<MasterLabelImageContent> GetImageAsync(
        Guid modelId, LabelType labelType, CancellationToken cancellationToken);
}
