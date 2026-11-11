using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionWorkflowService : IInspectionWorkflowService
{
    public InspectionWorkflowAssessment EvaluateFinalStatus(
        InspectionStatus? coStage,
        InspectionStatus? configStage,
        InspectionStatus? serialStage)
    {
        var stageStatuses = new[] { coStage, configStage, serialStage };
        if (stageStatuses.Contains(InspectionStatus.FAIL))
        {
            return new InspectionWorkflowAssessment(InspectionStatus.FAIL, "At least one inspection stage failed.");
        }

        if (stageStatuses.Contains(InspectionStatus.REVIEW_REQUIRED))
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "At least one inspection stage requires manual review.");
        }

        if (coStage is InspectionStatus.BLOCKED or InspectionStatus.NOT_APPLICABLE
            || configStage is InspectionStatus.BLOCKED or InspectionStatus.NOT_APPLICABLE
            || serialStage == InspectionStatus.BLOCKED)
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.BLOCKED,
                "Serial validation requires both CO and Config comparison stages to pass.");
        }

        if (coStage is null || configStage is null || serialStage is null
            || stageStatuses.Contains(InspectionStatus.PROCESSING))
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.PROCESSING,
                "All applicable inspection stages must complete before final evaluation.");
        }

        if (coStage != InspectionStatus.PASS || configStage != InspectionStatus.PASS)
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.BLOCKED,
                "Serial validation requires both CO and Config comparison stages to pass.");
        }

        if (serialStage == InspectionStatus.READY)
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.PROCESSING,
                "Serial validation has not completed.");
        }

        return serialStage is InspectionStatus.PASS or InspectionStatus.NOT_APPLICABLE
            ? new InspectionWorkflowAssessment(InspectionStatus.PASS, "All applicable inspection stages passed.")
            : new InspectionWorkflowAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "Serial validation did not complete with readable evidence.");
    }

    public InspectionWorkflowAssessment EvaluateFinalStatus(
        InspectionStatus? coStage,
        InspectionStatus? configStage,
        FieldObservation? actualCoSerial,
        FieldObservation? actualConfigSerial,
        double serialMinimumConfidence)
    {
        var stageAssessment = EvaluateFinalStatus(coStage, configStage, InspectionStatus.NOT_APPLICABLE);
        if (stageAssessment.Status != InspectionStatus.PASS)
            return stageAssessment;

        if (!IsReadable(actualCoSerial, serialMinimumConfidence)
            && !IsReadable(actualConfigSerial, serialMinimumConfidence))
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "Available serial-number evidence is missing or below the configured confidence threshold.");
        }

        if (!IsReadable(actualCoSerial, serialMinimumConfidence)
            || !IsReadable(actualConfigSerial, serialMinimumConfidence))
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "A serial number is available on only one label or is below the configured confidence threshold.");
        }

        var serialsMatch = string.Equals(
            actualCoSerial!.Value!.Trim(),
            actualConfigSerial!.Value!.Trim(),
            StringComparison.OrdinalIgnoreCase);

        return serialsMatch
            ? new InspectionWorkflowAssessment(InspectionStatus.PASS, "Both label stages passed and serial numbers match.")
            : new InspectionWorkflowAssessment(InspectionStatus.FAIL, "CO and Config serial numbers do not match.");
    }

    private static bool IsReadable(FieldObservation? observation, double minimumConfidence) =>
        observation is not null
        && !string.IsNullOrWhiteSpace(observation.Value)
        && observation.Confidence is >= 0 and <= 1
        && observation.Confidence >= minimumConfidence;
}
