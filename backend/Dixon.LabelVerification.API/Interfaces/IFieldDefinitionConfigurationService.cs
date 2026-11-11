using Dixon.LabelVerification.API.DTOs.Configuration;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IFieldDefinitionConfigurationService
{
    Task<IReadOnlyList<FieldDefinitionResponse>> GetAllAsync(
        Guid? labelProfileId,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<FieldDefinitionResponse>> FindByOcrNameAsync(
        Guid labelProfileId,
        string ocrName,
        CancellationToken cancellationToken);
    Task<FieldDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<FieldDefinitionResponse> CreateAsync(
        Guid labelProfileId,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken);
    Task<FieldDefinitionResponse> UpdateAsync(
        Guid id,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}
