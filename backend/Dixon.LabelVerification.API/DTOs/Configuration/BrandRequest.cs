using System.ComponentModel.DataAnnotations;

namespace Dixon.LabelVerification.API.DTOs.Configuration;

public sealed class BrandRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9._-]*$")]
    public string Code { get; init; } = string.Empty;

    [Required, StringLength(150, MinimumLength = 1)]
    public string Name { get; init; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; init; }

    public bool IsActive { get; init; } = true;
}

public sealed record BrandResponse(
    Guid Id, string Code, string Name, string? Description, bool IsActive, DateTimeOffset CreatedAtUtc);
