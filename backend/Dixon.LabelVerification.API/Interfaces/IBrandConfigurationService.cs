using Dixon.LabelVerification.API.DTOs.Configuration;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IBrandConfigurationService
{
    Task<IReadOnlyList<BrandResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<BrandResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<BrandResponse> CreateAsync(BrandRequest request, CancellationToken cancellationToken);
    Task<BrandResponse> UpdateAsync(Guid id, BrandRequest request, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
