using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs;

public sealed record FieldComparisonAssessment(InspectionStatus Status, string Reason);
