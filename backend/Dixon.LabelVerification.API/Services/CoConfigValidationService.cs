using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;

namespace Dixon.LabelVerification.API.Services;

public static class CoConfigValidationService
{
    private static readonly (string CoField, string ConfigField, string ComparisonKey)[] RequiredPairs =
    [
        ("MODEL_NO", "MODEL_NO", "MODEL_NO"),
        ("PART_NUMBER", "QUEUE_NO", "PART_NUMBER_TO_QUEUE_NO")
    ];

    public static StageResult Create(
        Guid inspectionId,
        IReadOnlyList<FieldDefinition> coFields,
        IReadOnlyList<FieldDefinition> configFields,
        IReadOnlyDictionary<Guid, ConfiguredFieldExtraction> coActual,
        IReadOnlyDictionary<Guid, ConfiguredFieldExtraction> configActual,
        DateTimeOffset? startedAtUtc,
        bool labelStagesPassed,
        IFieldComparisonService comparisonService)
    {
        var comparisons = new List<FieldComparison>(RequiredPairs.Length);
        foreach (var (coKey, configKey, comparisonKey) in RequiredPairs)
        {
            var coField = coFields.SingleOrDefault(field =>
                field.IsActive && string.Equals(field.FieldName, coKey, StringComparison.OrdinalIgnoreCase));
            var configField = configFields.SingleOrDefault(field =>
                field.IsActive && string.Equals(field.FieldName, configKey, StringComparison.OrdinalIgnoreCase));

            var coExtraction = coField is null ? null : coActual.GetValueOrDefault(coField.Id);
            var configExtraction = configField is null ? null : configActual.GetValueOrDefault(configField.Id);
            var coObservation = coExtraction?.Observation;
            var configObservation = configExtraction?.Observation;
            var status = InspectionStatus.REVIEW_REQUIRED;
            var reason = $"The {coKey} and {configKey} fields must be configured and readable.";

            if (!labelStagesPassed)
            {
                status = InspectionStatus.BLOCKED;
                reason = "COO / CONFIG validation was not run because both label comparison stages must pass.";
            }
            else if (coField is not null && configField is not null
                && coObservation is not null && configObservation is not null
                && IsReadable(coObservation, coField.OcrConfidenceThreshold)
                && IsReadable(configObservation, configField.OcrConfidenceThreshold))
            {
                var assessment = comparisonService.Compare(configField, coObservation, configObservation);
                status = assessment.Status;
                reason = assessment.Reason;
            }
            else if (coField is not null && configField is not null)
            {
                reason = coObservation is null || configObservation is null
                    ? $"The {coKey} or {configKey} value is unreadable."
                    : $"The {coKey} or {configKey} value is below its configured confidence threshold.";
            }

            comparisons.Add(new FieldComparison
            {
                FieldDefinitionId = configField?.Id,
                FieldKey = comparisonKey,
                MasterValue = coObservation?.Value,
                ActualValue = configObservation?.Value,
                NormalizedMasterValue = configField is null || coObservation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(configField, coObservation.Value!),
                NormalizedActualValue = configField is null || configObservation is null
                    ? null
                    : FieldComparisonService.NormalizeForStorage(configField, configObservation.Value!),
                ComparisonStrategy = configField?.ComparisonStrategy ?? FieldComparisonStrategy.Exact,
                MasterConfidence = coObservation?.Confidence,
                ActualConfidence = configObservation?.Confidence,
                Status = status,
                MismatchReason = reason
            });
        }

        var stageStatus = !labelStagesPassed
            ? InspectionStatus.BLOCKED
            : comparisons.Any(comparison => comparison.Status == InspectionStatus.FAIL)
                ? InspectionStatus.FAIL
                : comparisons.Any(comparison => comparison.Status == InspectionStatus.REVIEW_REQUIRED)
                    ? InspectionStatus.REVIEW_REQUIRED
                    : InspectionStatus.PASS;
        var stage = new StageResult
        {
            InspectionSessionId = inspectionId,
            LabelType = LabelType.CONFIG,
            StageType = InspectionStageType.SERIAL_VALIDATION,
            Status = stageStatus,
            TotalFields = comparisons.Count,
            MatchedFields = comparisons.Count(comparison => comparison.Status == InspectionStatus.PASS),
            MismatchedFields = comparisons.Count(comparison => comparison.Status == InspectionStatus.FAIL),
            UnreadableFields = comparisons.Count(comparison => comparison.Status == InspectionStatus.REVIEW_REQUIRED),
            StartedAtUtc = startedAtUtc,
            Remarks = "Validates actual COO Model No. against actual CONFIG Model No., and actual COO P/N against actual CONFIG Queue No."
        };
        foreach (var comparison in comparisons)
            stage.FieldComparisons.Add(comparison);
        return stage;
    }

    private static bool IsReadable(FieldObservation? observation, double minimumConfidence) =>
        observation is not null
        && !string.IsNullOrWhiteSpace(observation.Value)
        && observation.Confidence is >= 0 and <= 1
        && observation.Confidence >= minimumConfidence;
}
