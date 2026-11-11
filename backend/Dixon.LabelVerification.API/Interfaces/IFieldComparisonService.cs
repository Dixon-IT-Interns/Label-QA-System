using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Interfaces;

public interface IFieldComparisonService
{
    FieldComparisonAssessment Compare(
        FieldDefinition definition,
        FieldObservation? master,
        FieldObservation? actual);
}
