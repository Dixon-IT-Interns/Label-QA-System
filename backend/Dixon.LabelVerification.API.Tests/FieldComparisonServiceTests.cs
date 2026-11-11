using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;

namespace Dixon.LabelVerification.API.Tests;

public sealed class FieldComparisonServiceTests
{
    private readonly FieldComparisonService _service = new();

    [Fact]
    public void ExactComparisonDoesNotIgnoreCaseOrWhitespace()
    {
        var result = Compare("ABC", "abc", FieldComparisonStrategy.Exact);

        Assert.Equal(InspectionStatus.FAIL, result.Status);
    }

    [Fact]
    public void NormalizedTextCollapsesWhitespaceButDoesNotChangeCaseOrCharacters()
    {
        var whitespaceResult = Compare("  Model   123 ", "Model 123", FieldComparisonStrategy.NormalizedText);
        var caseResult = Compare("Model 123", "MODEL 123", FieldComparisonStrategy.NormalizedText);
        var ambiguousCharacterResult = Compare("O1", "01", FieldComparisonStrategy.NormalizedText);

        Assert.Equal(InspectionStatus.PASS, whitespaceResult.Status);
        Assert.Equal(InspectionStatus.FAIL, caseResult.Status);
        Assert.Equal(InspectionStatus.FAIL, ambiguousCharacterResult.Status);
    }

    [Fact]
    public void CaseInsensitiveComparisonIgnoresCaseButNotWhitespace()
    {
        var caseResult = Compare("Model 123", "MODEL 123", FieldComparisonStrategy.CaseInsensitive);
        var whitespaceResult = Compare("Model 123", " Model 123", FieldComparisonStrategy.CaseInsensitive);

        Assert.Equal(InspectionStatus.PASS, caseResult.Status);
        Assert.Equal(InspectionStatus.FAIL, whitespaceResult.Status);
    }

    [Theory]
    [InlineData("15-fd1654TU", "15-fd1652TU")]
    [InlineData("15-fd1654TU", "15-fd1689TU")]
    public void NormalizedModelComparisonPreservesMeaningfulModelDifferences(string master, string actual)
    {
        var result = Compare(master, actual, FieldComparisonStrategy.CaseInsensitive);

        Assert.Equal(InspectionStatus.FAIL, result.Status);
    }

    [Fact]
    public void ModelComparisonTreatsUnicodeDashAsFormattingButPreservesModelDigits()
    {
        var dashVariant = Compare("15-fd1654TU", "15—fd1654TU", FieldComparisonStrategy.CaseInsensitive);
        var differentModel = Compare("15-fd1654TU", "15—fd1652TU", FieldComparisonStrategy.CaseInsensitive);

        Assert.Equal(InspectionStatus.PASS, dashVariant.Status);
        Assert.Equal(InspectionStatus.FAIL, differentModel.Status);
    }

    [Fact]
    public void IdentifierComparisonIgnoresFormattingSeparators()
    {
        var result = Compare("ABC-123", "ABC123", FieldComparisonStrategy.Identifier);

        Assert.Equal(InspectionStatus.PASS, result.Status);
    }

    [Fact]
    public void MacAddressComparisonNormalizesSeparatorsAndReviewsInvalidValues()
    {
        var equivalent = Compare(
            "00:3C:B7:FF:23:EF",
            "003CB7FF23EF",
            FieldComparisonStrategy.MacAddress);
        var mismatch = Compare(
            "00:3C:B7:FF:23:EF",
            "00:3C:B7:FF:23:EE",
            FieldComparisonStrategy.MacAddress);
        var invalid = Compare("00:3C:B7:FF:23:EG", "003CB7FF23EG", FieldComparisonStrategy.MacAddress);

        Assert.Equal(InspectionStatus.PASS, equivalent.Status);
        Assert.Equal(InspectionStatus.FAIL, mismatch.Status);
        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, invalid.Status);
    }

    [Fact]
    public void NumericComparisonHonorsAbsoluteAndRelativeTolerance()
    {
        var absoluteResult = Compare(
            "100.00",
            "100.04",
            FieldComparisonStrategy.Numeric,
            absoluteTolerance: 0.05m);
        var relativeResult = Compare(
            "100",
            "100.9",
            FieldComparisonStrategy.Numeric,
            relativeTolerance: 0.01m);
        var outsideToleranceResult = Compare(
            "100",
            "102",
            FieldComparisonStrategy.Numeric,
            relativeTolerance: 0.01m);
        var invalidNumberResult = Compare("100V", "100", FieldComparisonStrategy.Numeric);

        Assert.Equal(InspectionStatus.PASS, absoluteResult.Status);
        Assert.Equal(InspectionStatus.PASS, relativeResult.Status);
        Assert.Equal(InspectionStatus.FAIL, outsideToleranceResult.Status);
        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, invalidNumberResult.Status);
    }

    [Fact]
    public void UnreadableOrLowConfidenceValuesRequireReview()
    {
        var missingResult = Compare(null, "123", FieldComparisonStrategy.Exact);
        var lowConfidenceResult = Compare(
            "123",
            "123",
            FieldComparisonStrategy.Exact,
            actualConfidence: 0.7);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, missingResult.Status);
        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, lowConfidenceResult.Status);
    }

    [Fact]
    public void OptionalFieldMissingFromBothLabelsIsNotApplicable()
    {
        var result = Compare(null, null, FieldComparisonStrategy.Exact, isRequired: false);

        Assert.Equal(InspectionStatus.NOT_APPLICABLE, result.Status);
    }

    [Fact]
    public void NonCriticalMismatchRequiresReviewRatherThanFail()
    {
        var result = Compare("first", "second", FieldComparisonStrategy.Exact, isCritical: false);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, result.Status);
    }

    [Fact]
    public void UnknownComparisonStrategyRequiresReview()
    {
        var result = Compare("same", "same", (FieldComparisonStrategy)99);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, result.Status);
    }

    [Fact]
    public void SerialComparisonCannotPassBeforeBothLabelStagesPass()
    {
        var result = new InspectionWorkflowService().EvaluateFinalStatus(
            InspectionStatus.READY,
            InspectionStatus.READY,
            null,
            null,
            0.9);

        Assert.Equal(InspectionStatus.BLOCKED, result.Status);
    }

    [Fact]
    public void MissingSerialEvidenceRequiresReviewAfterBothLabelStagesPass()
    {
        var result = new InspectionWorkflowService().EvaluateFinalStatus(
            InspectionStatus.PASS,
            InspectionStatus.PASS,
            null,
            null,
            0.9);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, result.Status);
    }

    [Fact]
    public void SerialComparisonCanPassWhenBothLabelStagesAndSerialEvidencePass()
    {
        var result = new InspectionWorkflowService().EvaluateFinalStatus(
            InspectionStatus.PASS,
            InspectionStatus.PASS,
            new FieldObservation { Value = "ABC123", Confidence = 0.99 },
            new FieldObservation { Value = "abc123", Confidence = 0.99 },
            0.9);

        Assert.Equal(InspectionStatus.PASS, result.Status);
    }

    [Fact]
    public void ConfiguredNotApplicableSerialStageCanPassAfterBothLabelsPass()
    {
        var result = new InspectionWorkflowService().EvaluateFinalStatus(
            InspectionStatus.PASS,
            InspectionStatus.PASS,
            InspectionStatus.NOT_APPLICABLE);

        Assert.Equal(InspectionStatus.PASS, result.Status);
    }

    [Theory]
    [InlineData(InspectionStatus.FAIL, InspectionStatus.PASS, InspectionStatus.FAIL)]
    [InlineData(InspectionStatus.PASS, InspectionStatus.REVIEW_REQUIRED, InspectionStatus.REVIEW_REQUIRED)]
    [InlineData(InspectionStatus.PROCESSING, InspectionStatus.PASS, InspectionStatus.PROCESSING)]
    public void FinalStatusRespectsComparisonStageTransitions(
        InspectionStatus coStage,
        InspectionStatus configStage,
        InspectionStatus expected)
    {
        var result = new InspectionWorkflowService().EvaluateFinalStatus(
            coStage,
            configStage,
            null,
            null,
            0.9);

        Assert.Equal(expected, result.Status);
    }

    private FieldComparisonAssessment Compare(
        string? masterValue,
        string? actualValue,
        FieldComparisonStrategy strategy,
        bool isRequired = true,
        bool isCritical = true,
        double actualConfidence = 0.99,
        decimal absoluteTolerance = 0,
        decimal relativeTolerance = 0)
    {
        var definition = new FieldDefinition
        {
            LabelProfileId = Guid.NewGuid(),
            FieldName = "TEST_FIELD",
            DisplayName = "Test field",
            Required = isRequired,
            Critical = isCritical,
            OcrConfidenceThreshold = 0.9,
            ComparisonStrategy = strategy,
            NumericAbsoluteTolerance = absoluteTolerance,
            NumericRelativeTolerance = relativeTolerance
        };
        var master = masterValue is null ? null : new FieldObservation { Value = masterValue, Confidence = 0.99 };
        var actual = actualValue is null
            ? null
            : new FieldObservation { Value = actualValue, Confidence = actualConfidence };

        return _service.Compare(definition, master, actual);
    }
}
