using System.ComponentModel.DataAnnotations;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs.Configuration;

public sealed class FieldDefinitionRequest : IValidatableObject
{
    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public string FieldName { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string DisplayName { get; init; } = string.Empty;

    [Required, RegularExpression("^(Text|Numeric|SerialNumber|Date)$")]
    public string DataType { get; init; } = "Text";

    public bool Required { get; init; } = true;
    public bool Critical { get; init; }

    [Range(0, 1)]
    public double OcrConfidenceThreshold { get; init; } = 0.9;

    [Range(0, int.MaxValue)]
    public int DisplayOrder { get; init; }

    public bool IsActive { get; init; } = true;

    [EnumDataType(typeof(FieldComparisonStrategy))]
    public FieldComparisonStrategy ComparisonStrategy { get; init; } = FieldComparisonStrategy.NormalizedText;

    [Range(typeof(decimal), "0", "999999999999")]
    public decimal NumericAbsoluteTolerance { get; init; }

    [Range(typeof(decimal), "0", "1")]
    public decimal NumericRelativeTolerance { get; init; }

    [MaxLength(20)]
    public List<string> Aliases { get; init; } = [];

    [MaxLength(20)]
    public List<FieldAliasInput> AliasDefinitions { get; init; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Aliases.Any(alias => string.IsNullOrWhiteSpace(alias) || alias.Length > 200))
        {
            yield return new ValidationResult(
                "Aliases must be non-empty and contain at most 200 characters.",
                [nameof(Aliases)]);
        }

        if (Aliases.Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase).Count() != Aliases.Count)
        {
            yield return new ValidationResult("Aliases must be unique, ignoring case.", [nameof(Aliases)]);
        }

        if (AliasDefinitions.Any(alias => string.IsNullOrWhiteSpace(alias.AliasText)
                || alias.AliasText.Length > 200
                || string.IsNullOrWhiteSpace(alias.Language)
                || alias.Language.Length > 20))
        {
            yield return new ValidationResult(
                "Each alias must have text (max 200 characters) and a language (max 20 characters).",
                [nameof(AliasDefinitions)]);
        }

        if (ComparisonStrategy != FieldComparisonStrategy.Numeric
            && (NumericAbsoluteTolerance != 0 || NumericRelativeTolerance != 0))
        {
            yield return new ValidationResult(
                "Numeric tolerances can only be set for the Numeric comparison strategy.",
                [nameof(NumericAbsoluteTolerance), nameof(NumericRelativeTolerance)]);
        }
    }
}

public sealed class FieldAliasInput
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string AliasText { get; init; } = string.Empty;

    [Required, StringLength(20, MinimumLength = 2)]
    public string Language { get; init; } = "en";

    public bool IsActive { get; init; } = true;
}

public sealed record FieldAliasResponse(Guid Id, string Value, string Language, bool IsActive);

public sealed record FieldDefinitionResponse(
    Guid Id,
    Guid LabelProfileId,
    LabelType LabelType,
    string FieldName,
    string DisplayName,
    string DataType,
    bool Required,
    bool Critical,
    double OcrConfidenceThreshold,
    int DisplayOrder,
    bool IsActive,
    FieldComparisonStrategy ComparisonStrategy,
    decimal NumericAbsoluteTolerance,
    decimal NumericRelativeTolerance,
    IReadOnlyList<FieldAliasResponse> Aliases,
    DateTimeOffset CreatedAtUtc);
