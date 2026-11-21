using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;

namespace Dixon.LabelVerification.API.Services;

public static class MasterLabelPairValidator
{
    private static readonly (string CoKey, string ConfigKey, string CoName, string ConfigName)[] RequiredPairs =
    [
        ("MODEL_NO", "MODEL_NO", "Model No.", "Model No."),
        ("PART_NUMBER", "QUEUE_NO", "P/N", "Queue No.")
    ];

    public static void Validate(
        IReadOnlyList<FieldDefinition> coDefinitions,
        IReadOnlyList<MasterLabelField> coFields,
        IReadOnlyList<FieldDefinition> configDefinitions,
        IReadOnlyList<MasterLabelField> configFields,
        IFieldComparisonService comparisonService)
    {
        foreach (var (coKey, configKey, coName, configName) in RequiredPairs)
        {
            var coDefinition = GetActiveDefinition(coDefinitions, coKey, coName);
            var configDefinition = GetActiveDefinition(configDefinitions, configKey, configName);
            var coField = GetField(coFields, coKey, "Master COO", coName);
            var configField = GetField(configFields, configKey, "Master Config", configName);

            if (!IsReadable(coField, coDefinition))
                throw new InspectionValidationException(
                    $"OCR could not confidently read Master COO {coName}. {coField.Reason} Check that the value is visible or encoded in its QR code; neither master label was saved.");
            if (!IsReadable(configField, configDefinition))
                throw new InspectionValidationException(
                    $"OCR could not confidently read Master Config {configName}. {configField.Reason} Neither master label was saved.");

            var coModelCode = coKey == "MODEL_NO" ? ModelCodeExtractor.Extract(coField.ExtractedValue) : null;
            var configModelCode = coKey == "MODEL_NO" ? ModelCodeExtractor.Extract(configField.ExtractedValue) : null;
            var matches = coModelCode is not null && configModelCode is not null
                ? string.Equals(coModelCode, configModelCode, StringComparison.OrdinalIgnoreCase)
                : comparisonService.Compare(
                    configDefinition,
                    new FieldObservation { Value = coField.ExtractedValue, Confidence = coField.Confidence },
                    new FieldObservation { Value = configField.ExtractedValue, Confidence = configField.Confidence })
                    .Status == InspectionStatus.PASS;
            if (!matches)
                throw new InspectionConflictException(
                    $"Master COO {coName} must match Master Config {configName}. Neither master label was saved.");
        }
    }

    private static FieldDefinition GetActiveDefinition(
        IReadOnlyList<FieldDefinition> definitions, string key, string displayName) =>
        definitions.SingleOrDefault(definition => definition.IsActive
            && string.Equals(definition.FieldName, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InspectionConflictException(
            $"The active label profiles must include {displayName} to validate the master labels.");

    private static MasterLabelField GetField(
        IReadOnlyList<MasterLabelField> fields, string key, string labelName, string displayName) =>
        fields.SingleOrDefault(field =>
            string.Equals(field.FieldKey, key, StringComparison.OrdinalIgnoreCase))
        ?? throw new InspectionValidationException(
            $"OCR could not extract {labelName} {displayName}. Neither master label was saved.");

    private static bool IsReadable(MasterLabelField field, FieldDefinition definition) =>
        !string.IsNullOrWhiteSpace(field.ExtractedValue)
        && double.IsFinite(field.Confidence)
        && field.Confidence >= definition.OcrConfidenceThreshold;
}
