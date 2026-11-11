using System.ComponentModel.DataAnnotations;

namespace Dixon.LabelVerification.API.DTOs.Configuration;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("A non-empty identifier is required.")
    {
    }

    public override bool IsValid(object? value) =>
        value is Guid guid && guid != Guid.Empty;
}
