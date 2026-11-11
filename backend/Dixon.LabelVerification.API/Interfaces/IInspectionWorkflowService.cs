using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionWorkflowService
{
    InspectionWorkflowAssessment EvaluateFinalStatus(
        InspectionStatus? coStage,
        InspectionStatus? configStage,
        InspectionStatus? serialStage);
}
