namespace Dixon.LabelVerification.API.Entities;

public enum FieldComparisonStrategy
{
    Exact,
    NormalizedText,
    CaseInsensitive,
    Numeric,
    Identifier,
    MacAddress
}
