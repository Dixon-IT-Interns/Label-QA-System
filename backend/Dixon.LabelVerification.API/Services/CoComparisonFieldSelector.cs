using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class CoComparisonFieldSelector
{
    private static readonly string[] RequiredFieldNames =
    [
        "SERIAL_NUMBER",
        "PART_NUMBER",
        "MODEL_NO",
        "QR_CODE"
    ];

    public static IReadOnlyList<FieldDefinition> Select(IEnumerable<FieldDefinition> definitions)
    {
        var fields = definitions
            .Where(definition => definition.IsActive
                && RequiredFieldNames.Contains(definition.FieldName, StringComparer.OrdinalIgnoreCase))
            .OrderBy(definition => Array.FindIndex(
                RequiredFieldNames,
                name => name.Equals(definition.FieldName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (fields.Count != RequiredFieldNames.Length
            || fields.Select(field => field.FieldName).Distinct(StringComparer.OrdinalIgnoreCase).Count()
                != RequiredFieldNames.Length)
        {
            throw new InspectionProcessingException(
                "The pinned CO profile must have exactly one active SERIAL_NUMBER, PART_NUMBER, MODEL_NO, and QR_CODE field.");
        }

        return fields;
    }
}
