using System.Text.Json;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;

namespace Dixon.LabelVerification.API.Tests;

public sealed class ConfiguredFieldExtractionTests
{
    [Fact]
    public void ExtractsOnlyAConfiguredLabelWithAnExplicitValueDelimiter()
    {
        var field = CreateField("SERIAL_NUMBER", "Serial Number", "S/N");
        var result = ConfiguredFieldExtractionService.Extract(
            field,
            [new OcrLineEvidence("S/N: O1-234", 0.96, null)]);

        Assert.Equal("O1-234", result.Observation?.Value);
        Assert.Equal(0.96, result.Observation?.Confidence);
    }

    [Fact]
    public void AmbiguousOrUndelimitedTextRemainsUnreadable()
    {
        var field = CreateField("MODEL_ID", "Model ID", "Model");
        var ambiguous = ConfiguredFieldExtractionService.Extract(
            field,
            [
                new OcrLineEvidence("Model ID: A100", 0.98, null),
                new OcrLineEvidence("Model: A100", 0.98, null)
            ]);
        var undelimited = ConfiguredFieldExtractionService.Extract(
            field,
            [new OcrLineEvidence("Model ID A100", 0.98, null)]);

        Assert.Null(ambiguous.Observation);
        Assert.Contains("Multiple", ambiguous.Reason);
        Assert.Null(undelimited.Observation);
    }

    [Fact]
    public void OcrResponseDeserializesSnakeCaseVersionMetadata()
    {
        const string payload = """
            {
              "status": "PROCESSING",
              "engine": "PaddleOCR",
              "engine_version": "3.0",
              "image_quality": { "width": 640, "height": 480, "blur_score": 120.5 },
              "lines": [{ "text": "Serial Number: O1", "confidence": 0.98, "box": null }],
              "message": "done"
            }
            """;

        var response = JsonSerializer.Deserialize<OcrServiceResponse>(payload);

        Assert.NotNull(response);
        Assert.Equal("3.0", response.EngineVersion);
        Assert.Equal("O1", ConfiguredFieldExtractionService.Extract(
            CreateField("SERIAL", "Serial Number"),
            response.Lines).Observation?.Value);
    }

    [Fact]
    public void UsesConfiguredSpatialCandidateFromOcrService()
    {
        var field = CreateField("SERIAL_NUMBER", "Serial Number", "S/N");
        var candidate = new OcrFieldCandidate(
            "SERIAL_NUMBER",
            "O1-234",
            0.96,
            [[120, 10], [200, 10], [200, 30], [120, 30]],
            "DETECTED",
            "Label and value were associated on one row.");

        var result = ConfiguredFieldExtractionService.Extract(
            field,
            [new OcrLineEvidence("S/N", 0.98, null)],
            [candidate]);

        Assert.Equal("O1-234", result.Observation?.Value);
        Assert.Equal(0.96, result.Observation?.Confidence);
        Assert.NotNull(result.BoundingBoxJson);
    }

    [Fact]
    public void DoesNotFallBackToTextWhenConfiguredCandidateIsAmbiguous()
    {
        var field = CreateField("SERIAL_NUMBER", "Serial Number");
        var candidate = new OcrFieldCandidate(
            "SERIAL_NUMBER", null, 0, null, "AMBIGUOUS", "Two possible values were detected.");

        var result = ConfiguredFieldExtractionService.Extract(
            field,
            [new OcrLineEvidence("Serial Number: ABC123", 0.99, null)],
            [candidate]);

        Assert.Null(result.Observation);
        Assert.Contains("Two possible values", result.Reason);
    }

    private static FieldDefinition CreateField(string key, string displayName, params string[] aliases)
    {
        var field = new FieldDefinition
        {
            LabelProfileId = Guid.NewGuid(),
            FieldName = key,
            DisplayName = displayName
        };
        field.Aliases = aliases.Select(alias => new FieldAlias
        {
            FieldDefinitionId = field.Id,
            Value = alias,
            Language = "en",
            IsActive = true
        }).ToList();
        return field;
    }
}
