using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs;

public sealed record InspectionWorkflowAssessment(InspectionStatus Status, string Reason);
