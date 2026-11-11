using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.DTOs.Configuration;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public sealed class FieldDefinitionConfigurationService(
    LabelVerificationDbContext dbContext,
    ILogger<FieldDefinitionConfigurationService> logger) : IFieldDefinitionConfigurationService
{
    public async Task<IReadOnlyList<FieldDefinitionResponse>> GetAllAsync(
        Guid? labelProfileId,
        CancellationToken cancellationToken)
    {
        var query = GetFieldQuery();
        if (labelProfileId.HasValue)
        {
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
        await EnsureProfileExistsAsync(labelProfileId, cancellationToken);
        await EnsureProfileNotPinnedAsync(labelProfileId, cancellationToken);
        var key = request.FieldName.Trim().ToUpperInvariant();
        await EnsureUniqueKeyAsync(labelProfileId, key, null, cancellationToken);

        var field = CreateEntity(labelProfileId, request, key);
        dbContext.FieldDefinitions.Add(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        await LoadReferencesAsync(field, cancellationToken);
        logger.LogInformation(
            "Created field {FieldDefinitionId} ({FieldKey}) on label profile {LabelProfileId}",
            field.Id,
            field.FieldName,
            labelProfileId);
        return ToResponse(field);
    }

    public async Task<FieldDefinitionResponse> UpdateAsync(
        Guid id,
        FieldDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var field = await dbContext.FieldDefinitions
            .Include(x => x.Aliases)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Field definition '{id}' was not found.");
        await EnsureProfileNotPinnedAsync(field.LabelProfileId, cancellationToken);
        var key = request.FieldName.Trim().ToUpperInvariant();
        await EnsureUniqueKeyAsync(field.LabelProfileId, key, id, cancellationToken);

        field.FieldName = key;
        field.DisplayName = request.DisplayName.Trim();
        field.DataType = request.DataType;
        ApplySettings(field, request);
        dbContext.FieldAliases.RemoveRange(field.Aliases);
        field.Aliases = CreateAliases(GetAliases(request));
        await dbContext.SaveChangesAsync(cancellationToken);
        await LoadReferencesAsync(field, cancellationToken);
        logger.LogInformation("Updated field definition {FieldDefinitionId}", id);
        return ToResponse(field);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var field = await dbContext.FieldDefinitions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ConfigurationNotFoundException($"Field definition '{id}' was not found.");
        await EnsureProfileNotPinnedAsync(field.LabelProfileId, cancellationToken);
        if (await dbContext.FieldComparisons.AnyAsync(x => x.FieldDefinitionId == id, cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This field is referenced by saved comparisons and cannot be deleted.");
        }

        dbContext.FieldDefinitions.Remove(field);
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Deleted field definition {FieldDefinitionId}", id);
    }

    private IQueryable<FieldDefinition> GetFieldQuery() =>
        dbContext.FieldDefinitions.AsNoTracking()
            .Include(x => x.LabelProfile)
            .Include(x => x.Aliases);

    private async Task EnsureProfileExistsAsync(Guid labelProfileId, CancellationToken cancellationToken)
    {
        if (!await dbContext.LabelProfiles.AnyAsync(x => x.Id == labelProfileId, cancellationToken))
        {
            throw new ConfigurationNotFoundException($"Label profile '{labelProfileId}' was not found.");
        }
    }

    private async Task EnsureProfileNotPinnedAsync(Guid labelProfileId, CancellationToken cancellationToken)
    {
        if (await dbContext.InspectionSessions.AnyAsync(
                x => x.LabelProfileCOId == labelProfileId || x.LabelProfileConfigId == labelProfileId,
                cancellationToken))
        {
            throw new ConfigurationConflictException(
                "This profile version is pinned by inspection history. Deactivate it and create a new profile version instead.");
        }
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

    private async Task LoadReferencesAsync(FieldDefinition field, CancellationToken cancellationToken)
    {
        await dbContext.Entry(field).Reference(x => x.LabelProfile).LoadAsync(cancellationToken);
        await dbContext.Entry(field).Collection(x => x.Aliases).LoadAsync(cancellationToken);
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
        new(
            field.Id,
            field.LabelProfileId,
            field.LabelProfile.LabelType,
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
