using Dixon.LabelVerification.API.DTOs.Configuration;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IModelConfigurationService
{
    Task<IReadOnlyList<ModelResponse>> GetAllAsync(Guid? brandId, CancellationToken cancellationToken);
    Task<ModelResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<ModelResponse> CreateAsync(ModelRequest request, CancellationToken cancellationToken);
    Task<ModelResponse> UpdateAsync(Guid id, ModelRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
