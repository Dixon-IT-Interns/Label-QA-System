using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dixon.LabelVerification.API.Services;

public static class DefaultLabelProfileProvisioner
{
    public static async Task<LabelProfile> EnsureActiveProfileAsync(
        LabelVerificationDbContext db,
        LaptopModel model,
        LabelType labelType,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);
        var profile = await db.LabelProfiles
            .Include(x => x.FieldDefinitions).ThenInclude(x => x.Aliases)
            .SingleOrDefaultAsync(
                x => x.BrandId == model.BrandId
                    && x.ModelId == model.Id
                    && x.LabelType == labelType
                    && x.IsActive,
                cancellationToken);

        if (profile is not null && HasDefaultFields(profile, labelType))
        {
            await transaction.CommitAsync(cancellationToken);
            return profile;
        }

        if (profile is not null)
        {
            profile.IsActive = false;
            await db.SaveChangesAsync(cancellationToken);
        }

        var version = (await db.LabelProfiles
            .Where(x => x.BrandId == model.BrandId
                && x.ModelId == model.Id
                && x.LabelType == labelType)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken) ?? 0) + 1;
        var defaultProfile = CreateDefaultProfile(model, labelType, version);
        db.LabelProfiles.Add(defaultProfile);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return defaultProfile;
    }

    public static LabelProfile CreateDefaultProfile(
        LaptopModel model,
        LabelType labelType,
        int version)
    {
        var profileName = labelType switch
        {
            LabelType.CO => "Default COO fields",
            LabelType.CONFIG => "Default Config fields",
            _ => throw new ArgumentOutOfRangeException(nameof(labelType), labelType, null)
        };
        var profile = new LabelProfile
        {
            BrandId = model.BrandId,
            ModelId = model.Id,
            LabelType = labelType,
            Code = $"DEFAULT-{labelType}",
            Name = profileName,
            Version = version,
            IsActive = true,
            EffectiveFromUtc = DateTimeOffset.UtcNow
        };

        foreach (var specification in GetFieldSpecifications(labelType))
        {
            var field = new FieldDefinition
            {
                LabelProfileId = profile.Id,
                FieldName = specification.Key,
                DisplayName = specification.DisplayName,
                DataType = "Text",
                Required = true,
                Critical = true,
                OcrConfidenceThreshold = specification.ConfidenceThreshold,
                DisplayOrder = specification.DisplayOrder,
                IsActive = true,
                ComparisonStrategy = specification.ComparisonStrategy
            };
            foreach (var alias in specification.Aliases)
                field.Aliases.Add(new FieldAlias { Value = alias });
            profile.FieldDefinitions.Add(field);
        }

        return profile;
    }

    private static bool HasDefaultFields(LabelProfile profile, LabelType labelType)
    {
        var activeKeys = profile.FieldDefinitions
            .Where(field => field.IsActive)
            .Select(field => field.FieldName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return GetFieldSpecifications(labelType).All(field => activeKeys.Contains(field.Key));
    }

    private static IReadOnlyList<DefaultFieldSpecification> GetFieldSpecifications(LabelType labelType) =>
        labelType switch
        {
            LabelType.CO =>
            [
                new("MODEL_NO", "Model No.", 1, 0.55, FieldComparisonStrategy.CaseInsensitive,
                    ["Model", "Model Number", "Product Model"]),
                new("QR_CODE", "QR Code", 2, 0.65, FieldComparisonStrategy.Exact,
                    ["QR", "QRCode"]),
                new("PART_NUMBER", "P/N", 3, 0.65, FieldComparisonStrategy.Exact,
                    ["PN", "Part No", "Part Number"])
            ],
            LabelType.CONFIG =>
            [
                new("MODEL_NO", "Model No.", 1, 0.55, FieldComparisonStrategy.CaseInsensitive,
                    ["Model", "Model Number", "Product Model"]),
                new("QUEUE_NO", "Queue No.", 2, 0.55, FieldComparisonStrategy.CaseInsensitive,
                    ["Queue", "Queue Number", "Q No"])
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(labelType), labelType, null)
        };

    private sealed record DefaultFieldSpecification(
        string Key,
        string DisplayName,
        int DisplayOrder,
        double ConfidenceThreshold,
        FieldComparisonStrategy ComparisonStrategy,
        IReadOnlyList<string> Aliases);
}
