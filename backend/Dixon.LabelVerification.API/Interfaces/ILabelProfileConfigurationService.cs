using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface ILabelProfileConfigurationService
{
    Task<IReadOnlyList<LabelProfileResponse>> GetAllAsync(
        Guid? brandId,
        Guid? modelId,
        LabelType? labelType,
        CancellationToken cancellationToken);
    Task<LabelProfileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<LabelProfileResponse> CreateAsync(LabelProfileRequest request, CancellationToken cancellationToken);
    Task<LabelProfileResponse> UpdateAsync(Guid id, LabelProfileRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
