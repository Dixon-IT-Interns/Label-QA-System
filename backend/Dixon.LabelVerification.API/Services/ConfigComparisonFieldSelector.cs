using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class ConfigComparisonFieldSelector
{
    private static readonly HashSet<string> RequiredFieldNames = new(
        ["MODEL_NO", "QUEUE_NO"],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<FieldDefinition> Select(
        IEnumerable<FieldDefinition> definitions)
    {
        var fields = definitions
            .Where(definition => definition.IsActive && RequiredFieldNames.Contains(definition.FieldName))
            .OrderBy(definition => definition.FieldName.Equals("MODEL_NO", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();

        if (fields.Count != RequiredFieldNames.Count
            || fields.Select(field => field.FieldName).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != RequiredFieldNames.Count)
        {
            throw new InspectionProcessingException(
                "The pinned CONFIG profile must have exactly one active MODEL_NO field and one active QUEUE_NO field.");
        }

        return fields;
    }
}
