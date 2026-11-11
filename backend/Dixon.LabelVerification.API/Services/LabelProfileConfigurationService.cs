using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class LabelProfileConfigurationService(
    LabelVerificationDbContext dbContext,
    ILogger<LabelProfileConfigurationService> logger) : ILabelProfileConfigurationService
{
    public async Task<IReadOnlyList<LabelProfileResponse>> GetAllAsync(
        Guid? brandId,
        Guid? modelId,
        LabelType? labelType,
        CancellationToken cancellationToken)
    {
        var query = dbContext.LabelProfiles.AsNoTracking()
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .AsQueryable();

        if (brandId.HasValue)
        {
            query = query.Where(x => x.BrandId == brandId.Value);
        }

        if (modelId.HasValue)
        {
            query = query.Where(x => x.ModelId == modelId.Value);
        }

        if (labelType.HasValue)
        {
            query = query.Where(x => x.LabelType == labelType.Value);
        }

        var profiles = await query.OrderBy(x => x.Brand.Name).ThenBy(x => x.Name).ThenBy(x => x.Version)
            .ToListAsync(cancellationToken);
        return profiles.Select(ToResponse).ToList();
    }

    public async Task<LabelProfileResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await GetProfileQuery()
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Label profile '{id}' was not found.");
        return ToResponse(profile);
    }

    public async Task<LabelProfileResponse> CreateAsync(
        LabelProfileRequest request,
        CancellationToken cancellationToken)
    {
        await EnsureBrandAndModelAsync(request.BrandId, request.ModelId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(request, code, null, cancellationToken);
        await EnsureSingleActiveProfileAsync(request, null, cancellationToken);

        var profile = new LabelProfile
        {
            BrandId = request.BrandId,
            ModelId = request.ModelId,
            LabelType = request.LabelType,
            Code = code,
            Name = request.Name.Trim(),
            Version = request.Version,
            IsActive = request.IsActive,
            EffectiveFromUtc = request.EffectiveFromUtc ?? DateTimeOffset.UtcNow
        };
        dbContext.LabelProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        await LoadReferencesAsync(profile, cancellationToken);
        logger.LogInformation(
            "Created {LabelType} label profile {ProfileId} for brand {BrandId}",
            profile.LabelType,
            profile.Id,
            profile.BrandId);
        return ToResponse(profile);
    }

    public async Task<LabelProfileResponse> UpdateAsync(
        Guid id,
        LabelProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.LabelProfiles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Label profile '{id}' was not found.");
        var isPinned = await IsProfilePinnedAsync(id, cancellationToken);
        if (isPinned)
        {
            if (request.IsActive
                || request.BrandId != profile.BrandId
                || request.ModelId != profile.ModelId
                || request.LabelType != profile.LabelType
                || !string.Equals(request.Code.Trim(), profile.Code, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(request.Name.Trim(), profile.Name, StringComparison.Ordinal)
                || request.Version != profile.Version)
            {
                throw new ConfigurationConflictException(
                    "This profile version is referenced by inspection history. It can only be deactivated; create a new version for edits.");
            }

            profile.IsActive = false;
            await dbContext.SaveChangesAsync(cancellationToken);
            await LoadReferencesAsync(profile, cancellationToken);
            return ToResponse(profile);
        }
        await EnsureBrandAndModelAsync(request.BrandId, request.ModelId, cancellationToken);
        var code = request.Code.Trim().ToUpperInvariant();
        await EnsureUniqueAsync(request, code, id, cancellationToken);
        await EnsureSingleActiveProfileAsync(request, id, cancellationToken);

        profile.BrandId = request.BrandId;
        profile.ModelId = request.ModelId;
        profile.LabelType = request.LabelType;
        profile.Code = code;
        profile.Name = request.Name.Trim();
        profile.Version = request.Version;
        profile.IsActive = request.IsActive;
        profile.EffectiveFromUtc = request.EffectiveFromUtc ?? profile.EffectiveFromUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        await LoadReferencesAsync(profile, cancellationToken);
        logger.LogInformation("Updated label profile {ProfileId}", id);
        return ToResponse(profile);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var profile = await dbContext.LabelProfiles.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Label profile '{id}' was not found.");
        await EnsureProfileNotPinnedAsync(id, cancellationToken);
        if (await dbContext.FieldDefinitions.AnyAsync(x => x.LabelProfileId == id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This profile contains field definitions. Delete or move those fields before deleting the profile.");
        }

        dbContext.LabelProfiles.Remove(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted label profile {ProfileId}", id);
    }

    private IQueryable<LabelProfile> GetProfileQuery() =>
        dbContext.LabelProfiles.AsNoTracking()
            .Include(x => x.Brand)
            .Include(x => x.Model);

    private async Task EnsureBrandAndModelAsync(
        Guid brandId,
        Guid? modelId,
        CancellationToken cancellationToken)
    {
        if (brandId == Guid.Empty)
        {
            throw new ConfigurationConflictException("A valid brand is required.");
        }

        if (!await dbContext.Brands.AnyAsync(x => x.Id == brandId, cancellationToken))
        {
            throw new ConfigurationNotFoundException($"Brand '{brandId}' was not found.");
        }

        if (modelId.HasValue
            && !await dbContext.Models.AnyAsync(
                x => x.Id == modelId.Value && x.BrandId == brandId,
                cancellationToken))
        {
            throw new ConfigurationConflictException("The selected model does not belong to the selected brand.");
        }
    }

    private async Task EnsureUniqueAsync(
        LabelProfileRequest request,
        string code,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var duplicate = await dbContext.LabelProfiles.AnyAsync(
            x => x.BrandId == request.BrandId
                 && x.ModelId == request.ModelId
                 && x.LabelType == request.LabelType
                 && x.Code == code
                 && x.Version == request.Version
                 && x.Id != excludedId,
            cancellationToken);
        if (duplicate)
        {
            throw new ConfigurationConflictException(
                "A profile with this code, type, and version already exists for the selected brand/model.");
        }
    }

    private async Task EnsureSingleActiveProfileAsync(
        LabelProfileRequest request,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (request.IsActive && request.ModelId.HasValue
            && await dbContext.LabelProfiles.AnyAsync(x =>
                x.ModelId == request.ModelId
                && x.LabelType == request.LabelType
                && x.IsActive
                && x.Id != excludedId, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "Only one active profile per model and label type is allowed. Deactivate the current profile first.");
        }
    }

    private async Task EnsureProfileNotPinnedAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await IsProfilePinnedAsync(id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This profile version is referenced by inspection history and cannot be changed or deleted.");
        }
    }

    private Task<bool> IsProfilePinnedAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.InspectionSessions.AnyAsync(
            x => x.LabelProfileCOId == id || x.LabelProfileConfigId == id,
            cancellationToken);

    private async Task LoadReferencesAsync(LabelProfile profile, CancellationToken cancellationToken)
    {
        await dbContext.Entry(profile).Reference(x => x.Brand).LoadAsync(cancellationToken);
        if (profile.ModelId.HasValue)
        {
            await dbContext.Entry(profile).Reference(x => x.Model).LoadAsync(cancellationToken);
        }
    }

    private static LabelProfileResponse ToResponse(LabelProfile profile) =>
        new(
            profile.Id,
            profile.BrandId,
            profile.Brand.Name,
            profile.ModelId,
            profile.Model?.Name,
            profile.LabelType,
            profile.Code,
            profile.Name,
            profile.Version,
            profile.IsActive,
            profile.EffectiveFromUtc,
            profile.CreatedAtUtc);
}
