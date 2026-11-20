using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class InspectionImageRequirements
{
    public static IReadOnlyList<InspectionImageType> RequiredTypes { get; } =
    [
        InspectionImageType.MASTER_CO,
        InspectionImageType.ACTUAL_CO,
        InspectionImageType.MASTER_CONFIG,
        InspectionImageType.ACTUAL_CONFIG
    ];

    public static IReadOnlyList<InspectionImageType> OperatorUploadTypes { get; } =
    [
        InspectionImageType.ACTUAL_CO,
        InspectionImageType.ACTUAL_CONFIG
    ];

    public static IReadOnlyList<InspectionImageType> GetMissingTypes(
        IEnumerable<InspectionImageType> uploadedTypes)
    {
        ArgumentNullException.ThrowIfNull(uploadedTypes);
        var uploaded = uploadedTypes.ToHashSet();
        return RequiredTypes.Where(type => !uploaded.Contains(type)).ToArray();
    }
}
