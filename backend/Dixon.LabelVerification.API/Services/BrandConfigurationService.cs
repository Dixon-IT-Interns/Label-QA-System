using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class BrandConfigurationService(
    LabelVerificationDbContext dbContext,
    AuthenticatedUserContext currentUser,
    ILogger<BrandConfigurationService> logger,
    IActivityLogService activityLog) : IBrandConfigurationService
{
    public async Task<IReadOnlyList<BrandResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var brandsQuery = dbContext.Brands.AsNoTracking().AsQueryable();
        if (!currentUser.IsAdmin)
        {
            var brandId = currentUser.BrandId
                ?? throw new UnauthorizedAccessException("The authenticated account has no assigned brand.");
            brandsQuery = brandsQuery.Where(x => x.Id == brandId);
        }
        var brands = await brandsQuery
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
        return brands.Select(ToResponse).ToList();
    }

    public async Task<BrandResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var brand = await dbContext.Brands.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Brand '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(brand.Id);
        return ToResponse(brand);
    }

    public async Task<BrandResponse> CreateAsync(BrandRequest request, CancellationToken cancellationToken)
    {
        currentUser.EnsureAdmin();
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        await EnsureUniqueAsync(code, name, null, cancellationToken);

        var brand = new Brand
        {
            Code = code,
            Name = name,
            Description = request.Description?.Trim(),
            IsActive = request.IsActive
        };
        dbContext.Brands.Add(brand);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("BRAND_CREATED", $"Created brand {brand.Name}.", brand.Id, entityId: brand.Id, entityType: "Brand", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Created brand {BrandId} with code {BrandCode}", brand.Id, brand.Code);
        return ToResponse(brand);
    }

    public async Task<BrandResponse> UpdateAsync(
        Guid id,
        BrandRequest request,
        CancellationToken cancellationToken)
    {
        currentUser.EnsureAdmin();
        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Brand '{id}' was not found.");
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();
        await EnsureUniqueAsync(code, name, id, cancellationToken);

        brand.Code = code;
        brand.Name = name;
        brand.Description = request.Description?.Trim();
        brand.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("BRAND_UPDATED", $"Updated brand {brand.Name}.", brand.Id, entityId: id, entityType: "Brand", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Updated brand {BrandId}", id);
        return ToResponse(brand);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        currentUser.EnsureAdmin();
        var brand = await dbContext.Brands.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Brand '{id}' was not found.");
        if (await dbContext.Models.AnyAsync(x => x.BrandId == id, cancellationToken)
            || await dbContext.LabelProfiles.AnyAsync(x => x.BrandId == id, cancellationToken)
            || await dbContext.InspectionSessions.AnyAsync(x => x.BrandId == id, cancellationToken)
            || await dbContext.Users.AnyAsync(x => x.BrandId == id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This brand is assigned to users or referenced by models, label profiles, or inspections and cannot be deleted.");
        }

        dbContext.Brands.Remove(brand);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("BRAND_DELETED", $"Deleted brand {brand.Name}.", brand.Id, entityId: id, entityType: "Brand", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted brand {BrandId}", id);
    }

    private async Task EnsureUniqueAsync(
        string code,
        string name,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.Brands.AnyAsync(
            x => x.Id != excludedId
                 && (x.Code == code || x.Name.ToUpper() == name.ToUpper()),
            cancellationToken);
        if (duplicate)
        {
            throw new ConfigurationConflictException("A brand with this code or name already exists.");
        }
    }

    private static BrandResponse ToResponse(Brand brand) =>
        new(brand.Id, brand.Code, brand.Name, brand.Description, brand.IsActive, brand.CreatedAtUtc);
}
