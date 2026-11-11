using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Repositories;

public sealed class InspectionSessionRepository(LabelVerificationDbContext dbContext) : IInspectionSessionRepository
{
    public Task<InspectionSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.InspectionSessions
            .AsNoTracking()
            .Include(x => x.StageResults)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task AddAsync(InspectionSession inspection, CancellationToken cancellationToken)
    {
        await dbContext.InspectionSessions.AddAsync(inspection, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
