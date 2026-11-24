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
                "COO / CONFIG validation requires both label comparison stages to pass.");
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
                "COO / CONFIG validation requires both label comparison stages to pass.");
        }

        if (serialStage == InspectionStatus.READY)
        {
            return new InspectionWorkflowAssessment(
                InspectionStatus.PROCESSING,
                "COO / CONFIG validation has not completed.");
        }

        return serialStage is InspectionStatus.PASS or InspectionStatus.NOT_APPLICABLE
            ? new InspectionWorkflowAssessment(InspectionStatus.PASS, "All applicable inspection stages passed.")
            : new InspectionWorkflowAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "COO / CONFIG validation did not complete with readable evidence.");
    }
}
