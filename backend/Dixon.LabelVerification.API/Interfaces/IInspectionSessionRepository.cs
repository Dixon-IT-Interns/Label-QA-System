using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionSessionRepository
{
    Task<InspectionSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task AddAsync(InspectionSession inspection, CancellationToken cancellationToken);
}
