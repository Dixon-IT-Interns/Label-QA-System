using System.ComponentModel.DataAnnotations;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs.Configuration;

public sealed class LabelProfileRequest
{
    [NotEmptyGuid]
    public Guid BrandId { get; init; }

    public Guid? ModelId { get; init; }

    [EnumDataType(typeof(LabelType))]
    public LabelType LabelType { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int Version { get; init; } = 1;

    public DateTimeOffset? EffectiveFromUtc { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record LabelProfileResponse(
    Guid Id,
    Guid BrandId,
    string BrandName,
    Guid? ModelId,
    string? ModelName,
    LabelType LabelType,
    string Code,
    string Name,
    int Version,
    bool IsActive,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset CreatedAtUtc);
