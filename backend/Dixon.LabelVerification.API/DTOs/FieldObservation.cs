using System.ComponentModel.DataAnnotations;

namespace Dixon.LabelVerification.API.DTOs;

public sealed class FieldObservation
{
    public string? Value { get; init; }

    [Range(0, 1)]
    public double? Confidence { get; init; }
}
