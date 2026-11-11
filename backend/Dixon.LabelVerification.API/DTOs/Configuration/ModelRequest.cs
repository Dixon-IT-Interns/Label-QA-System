using System.ComponentModel.DataAnnotations;

namespace Dixon.LabelVerification.API.DTOs.Configuration;

public sealed class ModelRequest
{
    [NotEmptyGuid]
    public Guid BrandId { get; init; }

    [Required, StringLength(100, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(200, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record ModelResponse(
    Guid Id,
    Guid BrandId,
    string BrandName,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
