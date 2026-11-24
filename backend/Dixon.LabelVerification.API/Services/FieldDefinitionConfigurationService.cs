using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class FieldDefinitionConfigurationService(
    LabelVerificationDbContext dbContext,
    AuthenticatedUserContext currentUser,
    ILogger<FieldDefinitionConfigurationService> logger,
    IActivityLogService activityLog) : IFieldDefinitionConfigurationService
{
    public async Task<IReadOnlyList<FieldDefinitionResponse>> GetAllAsync(
        Guid? labelProfileId,
        CancellationToken cancellationToken)
    {
        var query = GetFieldQuery();
        if (!currentUser.IsAdmin)
        {
            var brandId = currentUser.BrandId
                ?? throw new UnauthorizedAccessException("The authenticated account has no assigned brand.");
            query = query.Where(x => x.LabelProfile.BrandId == brandId);
        }
        if (labelProfileId.HasValue)
        {
            await EnsureProfileExistsAsync(labelProfileId.Value, cancellationToken);
            query = query.Where(x => x.LabelProfileId == labelProfileId.Value);
        }

        var fields = await query.OrderBy(x => x.LabelProfileId).ThenBy(x => x.DisplayOrder).ThenBy(x => x.FieldName)
            .ToListAsync(cancellationToken);
        return fields.Select(ToResponse).ToList();
    }

    public async Task<FieldDefinitionResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var field = await GetFieldQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Field definition '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(field.LabelProfile.BrandId);
        return ToResponse(field);
    }

    public async Task<IReadOnlyList<FieldDefinitionResponse>> FindByOcrNameAsync(
        Guid labelProfileId,
        string ocrName,
        CancellationToken cancellationToken)
    {
        await EnsureProfileExistsAsync(labelProfileId, cancellationToken);
        var normalizedName = NormalizeOcrName(ocrName);
        if (normalizedName.Length == 0)
        {
            return [];
        }

        var fields = await GetFieldQuery()
            .Where(x => x.LabelProfileId == labelProfileId)
            .ToListAsync(cancellationToken);
        return fields
            .Where(field =>
                field.IsActive && (NormalizeOcrName(field.FieldName) == normalizedName
                || NormalizeOcrName(field.DisplayName) == normalizedName
                || field.Aliases.Any(alias => alias.IsActive && NormalizeOcrName(alias.Value) == normalizedName)))
            .Select(ToResponse)
            .ToList();
    }

    public async Task<FieldDefinitionResponse> CreateAsync(
        Guid labelProfileId,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        currentUser.EnsureCanManageConfiguration();
        var profile = await dbContext.LabelProfiles.SingleOrDefaultAsync(
            x => x.Id == labelProfileId, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Label profile '{labelProfileId}' was not found.");
        currentUser.EnsureCanAccessBrand(profile.BrandId);
        var profileWasForked = false;
        if (await IsProfilePinnedAsync(profile.Id, cancellationToken))
        {
            if (!profile.IsActive)
            {
                throw new ConfigurationConflictException(
                    "Only the active profile version can be edited. Select the active version to change its settings.");
            }

            profile = await CreateNextProfileVersionAsync(profile, cancellationToken);
            profileWasForked = true;
        }

        var key = request.FieldName.Trim().ToUpperInvariant();
        if (profileWasForked)
        {
            EnsureUniqueKey(profile, key, null);
        }
        else
        {
            await EnsureUniqueKeyAsync(profile.Id, key, null, cancellationToken);
        }

        var field = CreateEntity(profile.Id, request, key);
        dbContext.FieldDefinitions.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("CONFIG_FIELD_CREATED", $"Created field {field.FieldName}.", profile.BrandId, entityId: field.Id, entityType: "FieldDefinition", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Created field {FieldDefinitionId} ({FieldKey}) on label profile {LabelProfileId}",
            field.Id,
            field.FieldName,
            profile.Id);
        return ToResponse(field, profile.LabelType);
    }

    public async Task<FieldDefinitionResponse> UpdateAsync(
        Guid id,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var field = await dbContext.FieldDefinitions
            .Include(x => x.LabelProfile)
            .Include(x => x.Aliases)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Field definition '{id}' was not found.");
        currentUser.EnsureCanManageConfiguration();
        currentUser.EnsureCanAccessBrand(field.LabelProfile.BrandId);
        var labelType = field.LabelProfile.LabelType;
        LabelProfile? forkedProfile = null;
        if (await IsProfilePinnedAsync(field.LabelProfileId, cancellationToken))
        {
            if (!field.LabelProfile.IsActive)
            {
                throw new ConfigurationConflictException(
                    "Only the active profile version can be edited. Select the active version to change its settings.");
            }

            forkedProfile = await CreateNextProfileVersionAsync(field.LabelProfile, cancellationToken);
            field = forkedProfile.FieldDefinitions.Single(clone =>
                string.Equals(clone.FieldName, field.FieldName, StringComparison.OrdinalIgnoreCase));
            field.LabelProfileId = forkedProfile.Id;
            field.LabelProfile = forkedProfile;
            labelType = forkedProfile.LabelType;
        }

        var key = request.FieldName.Trim().ToUpperInvariant();
        if (forkedProfile is not null)
        {
            EnsureUniqueKey(forkedProfile, key, field.Id);
        }
        else
        {
            await EnsureUniqueKeyAsync(field.LabelProfileId, key, field.Id, cancellationToken);
        }

        field.FieldName = key;
        field.DisplayName = request.DisplayName.Trim();
        field.DataType = request.DataType;
        ApplySettings(field, request);
        dbContext.FieldAliases.RemoveRange(field.Aliases);
        field.Aliases = CreateAliases(GetAliases(request));
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("CONFIG_FIELD_UPDATED", $"Updated field {field.FieldName}.", field.LabelProfile.BrandId, entityId: field.Id, entityType: "FieldDefinition", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Updated field definition {FieldDefinitionId}", id);
        return ToResponse(field, labelType);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        currentUser.EnsureCanManageConfiguration();
        var field = await dbContext.FieldDefinitions.Include(x => x.LabelProfile)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Field definition '{id}' was not found.");
        currentUser.EnsureCanAccessBrand(field.LabelProfile.BrandId);
        if (await IsProfilePinnedAsync(field.LabelProfileId, cancellationToken))
        {
            if (!field.LabelProfile.IsActive)
            {
                throw new ConfigurationConflictException(
                    "Only the active profile version can be edited. Select the active version to change its settings.");
            }

            await CreateNextProfileVersionAsync(
                field.LabelProfile, cancellationToken, field.FieldName);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            logger.LogInformation("Deleted field {FieldDefinitionId} in a new profile version", id);
            return;
        }

        if (await dbContext.FieldComparisons.AnyAsync(x => x.FieldDefinitionId == id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This field is referenced by saved comparisons and cannot be deleted.");
        }

        dbContext.FieldDefinitions.Remove(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        await activityLog.RecordAsync("CONFIG_FIELD_DELETED", $"Deleted field {field.FieldName}.", field.LabelProfile.BrandId, entityId: id, entityType: "FieldDefinition", cancellationToken: cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation("Deleted field definition {FieldDefinitionId}", id);
    }

    private IQueryable<FieldDefinition> GetFieldQuery() =>
        dbContext.FieldDefinitions.AsNoTracking()
            .Include(x => x.LabelProfile)
            .Include(x => x.Aliases);

    private async Task EnsureProfileExistsAsync(Guid labelProfileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.LabelProfiles.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == labelProfileId, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Label profile '{labelProfileId}' was not found.");
        currentUser.EnsureCanAccessBrand(profile.BrandId);
    }

    private Task<bool> IsProfilePinnedAsync(Guid labelProfileId, CancellationToken cancellationToken) =>
        dbContext.InspectionSessions.AnyAsync(
            x => x.LabelProfileCOId == labelProfileId || x.LabelProfileConfigId == labelProfileId,
            cancellationToken);

    private static void EnsureUniqueKey(LabelProfile profile, string key, Guid? excludedId)
    {
        if (profile.FieldDefinitions.Any(field =>
                field.Id != excludedId
                && string.Equals(field.FieldName, key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConfigurationConflictException("A field with this key already exists on the selected profile.");
        }
    }

    private async Task<LabelProfile> CreateNextProfileVersionAsync(
        LabelProfile sourceProfile,
        CancellationToken cancellationToken,
        string? excludedFieldName = null)
    {
        var sourceFields = await dbContext.FieldDefinitions.AsNoTracking()
            .Include(x => x.Aliases)
            .Where(x => x.LabelProfileId == sourceProfile.Id
                && (excludedFieldName == null || x.FieldName != excludedFieldName))
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.FieldName)
            .ToListAsync(cancellationToken);
        var latestVersion = await dbContext.LabelProfiles
            .Where(x => x.BrandId == sourceProfile.BrandId
                && x.ModelId == sourceProfile.ModelId
                && x.LabelType == sourceProfile.LabelType
                && x.Code == sourceProfile.Code)
            .MaxAsync(x => x.Version, cancellationToken);

        sourceProfile.IsActive = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        var newProfile = new LabelProfile
        {
            BrandId = sourceProfile.BrandId,
            ModelId = sourceProfile.ModelId,
            LabelType = sourceProfile.LabelType,
            Code = sourceProfile.Code,
            Name = sourceProfile.Name,
            Version = checked(latestVersion + 1),
            IsActive = true,
            EffectiveFromUtc = DateTimeOffset.UtcNow
        };
        foreach (var sourceField in sourceFields)
        {
            newProfile.FieldDefinitions.Add(new FieldDefinition
            {
                LabelProfileId = newProfile.Id,
                LabelProfile = newProfile,
                FieldName = sourceField.FieldName,
                DisplayName = sourceField.DisplayName,
                DataType = sourceField.DataType,
                Required = sourceField.Required,
                Critical = sourceField.Critical,
                OcrConfidenceThreshold = sourceField.OcrConfidenceThreshold,
                DisplayOrder = sourceField.DisplayOrder,
                IsActive = sourceField.IsActive,
                ComparisonStrategy = sourceField.ComparisonStrategy,
                NumericAbsoluteTolerance = sourceField.NumericAbsoluteTolerance,
                NumericRelativeTolerance = sourceField.NumericRelativeTolerance,
                Aliases = sourceField.Aliases.Select(alias => new FieldAlias
                {
                    Value = alias.Value,
                    Language = alias.Language,
                    IsActive = alias.IsActive
                }).ToList()
            });
        }

        dbContext.LabelProfiles.Add(newProfile);
        return newProfile;
    }

    private async Task EnsureUniqueKeyAsync(
        Guid labelProfileId,
        string key,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.FieldDefinitions.AnyAsync(
                x => x.LabelProfileId == labelProfileId && x.Id != excludedId && x.FieldName == key,
                cancellationToken))
        {
            throw new ConfigurationConflictException("A field with this key already exists on the selected profile.");
        }
    }

    private static FieldDefinition CreateEntity(
        Guid labelProfileId,
        FieldDefinitionRequest request,
        string key)
    {
        var field = new FieldDefinition
        {
            LabelProfileId = labelProfileId,
            FieldName = key,
            DisplayName = request.DisplayName.Trim(),
            DataType = request.DataType
        };
        ApplySettings(field, request);
        field.Aliases = CreateAliases(GetAliases(request));
        return field;
    }

    private static void ApplySettings(FieldDefinition field, FieldDefinitionRequest request)
    {
        field.Required = request.Required;
        field.Critical = request.Critical;
        field.OcrConfidenceThreshold = request.OcrConfidenceThreshold;
        field.DisplayOrder = request.DisplayOrder;
        field.IsActive = request.IsActive;
        field.ComparisonStrategy = request.ComparisonStrategy;
        field.NumericAbsoluteTolerance = request.NumericAbsoluteTolerance;
        field.NumericRelativeTolerance = request.NumericRelativeTolerance;
    }

    private static IEnumerable<FieldAliasInput> GetAliases(FieldDefinitionRequest request) =>
        request.AliasDefinitions.Count > 0
            ? request.AliasDefinitions
            : request.Aliases.Select(alias => new FieldAliasInput { AliasText = alias });

    private static ICollection<FieldAlias> CreateAliases(IEnumerable<FieldAliasInput> aliases) =>
        aliases
            .Select(alias => new
            {
                Value = alias.AliasText.Trim(),
                Language = alias.Language.Trim(),
                alias.IsActive
            })
            .Where(alias => alias.Value.Length > 0)
            .DistinctBy(alias => (alias.Value.ToUpperInvariant(), alias.Language.ToUpperInvariant()))
            .Select(alias => new FieldAlias
            {
                Value = alias.Value,
                Language = alias.Language,
                IsActive = alias.IsActive
            })
            .ToList();

    private static string NormalizeOcrName(string value) =>
        string.Join(
            ' ',
            value.Trim().Normalize(System.Text.NormalizationForm.FormKC)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .ToUpperInvariant();

    private static FieldDefinitionResponse ToResponse(FieldDefinition field) =>
        ToResponse(field, field.LabelProfile.LabelType);

    private static FieldDefinitionResponse ToResponse(FieldDefinition field, LabelType labelType) =>
        new(
            field.Id,
            field.LabelProfileId,
            labelType,
            field.FieldName,
            field.DisplayName,
            field.DataType,
            field.Required,
            field.Critical,
            field.OcrConfidenceThreshold,
            field.DisplayOrder,
            field.IsActive,
            field.ComparisonStrategy,
            field.NumericAbsoluteTolerance,
            field.NumericRelativeTolerance,
            field.Aliases.OrderBy(x => x.Value)
                .Select(x => new FieldAliasResponse(x.Id, x.Value, x.Language, x.IsActive)).ToList(),
            field.CreatedAtUtc);
}
