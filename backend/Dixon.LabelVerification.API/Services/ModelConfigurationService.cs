using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class ModelConfigurationService(
    LabelVerificationDbContext dbContext,
    AuthenticatedUserContext currentUser,
    ILogger<ModelConfigurationService> logger) : IModelConfigurationService
{
    public async Task<IReadOnlyList<ModelResponse>> GetAllAsync(
        Guid? brandId,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Models.AsNoTracking().Include(x => x.Brand).AsQueryable();
        if (brandId.HasValue)
        {
            currentUser.EnsureCanAccessBrand(brandId.Value);
            query = query.Where(x => x.BrandId == brandId.Value);
        }
        else if (!currentUser.IsAdmin)
        {
            var assignedBrandId = currentUser.BrandId
                ?? throw new UnauthorizedAccessException("The authenticated account has no assigned brand.");
            query = query.Where(x => x.BrandId == assignedBrandId);
        }

        var models = await query.OrderBy(x => x.Brand.Name).ThenBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return models.Select(ToResponse).ToList();
    }

    public async Task<ModelResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var model = await dbContext.Models.AsNoTracking().Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Model '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(model.BrandId);
        return ToResponse(model);
    }

    public async Task<ModelResponse> CreateAsync(ModelRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureCanManageConfiguration();
        currentUser.EnsureCanAccessBrand(request.BrandId);
        await EnsureBrandExistsAsync(request.BrandId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(request.BrandId, code, null, cancellationToken);

        var model = new LaptopModel
        {
            BrandId = request.BrandId,
            Code = code,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };
        dbContext.Models.Add(model);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(model).Reference(x => x.Brand).LoadAsync(cancellationToken);
        logger.LogInformation("Created model {ModelId} for brand {BrandId}", model.Id, model.BrandId);
        return ToResponse(model);
    }

    public async Task<ModelResponse> UpdateAsync(
        Guid id,
        ModelRequest request,
        CancellationToken cancellationToken)
    {
        var model = await dbContext.Models.Include(x => x.Brand)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Model '{id}' was not found.");
        currentUser.EnsureCanManageConfiguration();
        currentUser.EnsureCanAccessBrand(model.BrandId);
        currentUser.EnsureCanAccessBrand(request.BrandId);
        await EnsureBrandExistsAsync(request.BrandId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(request.BrandId, code, id, cancellationToken);

        model.BrandId = request.BrandId;
        model.Brand = await dbContext.Brands.SingleAsync(x => x.Id == request.BrandId, cancellationToken);
        model.Code = code;
        model.Name = request.Name.Trim();
        model.Description = request.Description?.Trim();
        model.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(model).Reference(x => x.Brand).LoadAsync(cancellationToken);
        logger.LogInformation("Updated model {ModelId}", id);
        return ToResponse(model);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        currentUser.EnsureCanManageConfiguration();
        var model = await dbContext.Models.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Model '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(model.BrandId);
        if (await dbContext.LabelProfiles.AnyAsync(x => x.ModelId == id, cancellationToken)
            || await dbContext.InspectionSessions.AnyAsync(x => x.ModelId == id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This model is referenced by label profiles or inspections and cannot be deleted.");
        }

        dbContext.Models.Remove(model);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted model {ModelId}", id);
    }

    private async Task EnsureBrandExistsAsync(Guid brandId, CancellationToken cancellationToken)
    {
        if (brandId == Guid.Empty
            || !await dbContext.Brands.AnyAsync(x => x.Id == brandId, cancellationToken))
        {
            throw new ConfigurationNotFoundException($"Brand '{brandId}' was not found.");
        }
    }

    private async Task EnsureUniqueAsync(
        Guid brandId,
        string code,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Models.AnyAsync(
                x => x.BrandId == brandId && x.Id != excludedId && x.Code == code,
                cancellationToken))
        {
            throw new ConfigurationConflictException("A model with this code already exists for the selected brand.");
        }
    }

    private static ModelResponse ToResponse(LaptopModel model) =>
        new(model.Id, model.BrandId, model.Brand.Name, model.Code, model.Name,
            model.Description, model.IsActive, model.CreatedAtUtc);
}
