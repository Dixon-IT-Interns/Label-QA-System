using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Services;

namespace Dixon.LabelVerification.API.Tests;

public sealed class LaptopLabelPipelineTests
{
    private readonly FieldComparisonService _comparisonService = new();

    [Fact]
    public void ExactHpSpecificationValuesMatchAcrossConfiguredFields()
    {
        var values = new Dictionary<string, string>
        {
            ["MODEL"] = "HP Laptop 15-fd1654TU",
            ["PROCESSOR"] = "Intel Core 5 120U Processor",
            ["STORAGE"] = "512GB Solid State Drive",
            ["RAM"] = "8GB SODIMM DDR4 SDRAM",
            ["OPERATING_SYSTEM"] = "Windows 11",
            ["DISPLAY"] = "15.6\" FHD LED Display (250 nits)",
            ["BATTERY"] = "3-cell, 41 Wh"
        };

        var results = values.Select(pair => Compare(pair.Key, pair.Value, pair.Value)).ToList();

        Assert.Equal(7, results.Count);
        Assert.All(results, result => Assert.Equal(InspectionStatus.PASS, result.Status));
    }

    [Theory]
    [InlineData("STORAGE", "512GB Solid State Drive", "512 GB SSD")]
    [InlineData("PROCESSOR", "Intel® Core™ 5 120U Processor", "Intel Core 5 120U")]
    [InlineData("BATTERY", "3-call, 41 Wh", "3-cell 41 wh")]
    [InlineData("DISPLAY", "15.6\" FHD LED Display (250 nits)", "15.6 FHD LED 250 nits")]
    public void MinorOcrAndFormattingDifferencesNormalizeToMatch(
        string fieldName,
        string master,
        string actual)
    {
        var result = Compare(fieldName, master, actual);

        Assert.Equal(InspectionStatus.PASS, result.Status);
    }

    [Fact]
    public void GenuineStorageMismatchIsReportedAsFailure()
    {
        var result = Compare("STORAGE", "512GB Solid State Drive", "1TB Solid State Drive");

        Assert.Equal(InspectionStatus.FAIL, result.Status);
    }

    [Fact]
    public void MissingRequiredConfiguredFieldRequiresReview()
    {
        var field = CreateField("BATTERY", "Battery");

        var result = _comparisonService.Compare(
            field,
            new FieldObservation { Value = "3-cell, 41 Wh", Confidence = 0.95 },
            null);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, result.Status);
    }

    [Fact]
    public void LowConfidenceIrrelevantOcrDoesNotLowerConfiguredFieldConfidence()
    {
        var field = CreateField("STORAGE", "Storage");
        var extracted = ConfiguredFieldExtractionService.Extract(
            field,
            [
                new OcrLineEvidence("For the disclaimer visit HP.com", 0.02, null),
                new OcrLineEvidence("Y28% @ ©", 0.01, null)
            ],
            [
                new OcrFieldCandidate(
                    "STORAGE", "512GB Solid State Drive", 0.93, null, "DETECTED", "Storage specification found.")
            ]);

        var result = _comparisonService.Compare(
            field,
            extracted.Observation,
            new FieldObservation { Value = "512 GB SSD", Confidence = 0.92 });

        Assert.Equal(0.93, extracted.Observation?.Confidence);
        Assert.Equal(InspectionStatus.PASS, result.Status);
    }

    [Fact]
    public void HandwrittenAnnotationAndBarcodeAreNotComparisonFields()
    {
        var fields = new[]
        {
            CreateField("MODEL", "Model"),
            CreateField("STORAGE", "Storage")
        };
        var masterValues = new[] { "15-fd1654TU", "512GB Solid State Drive" };
        var actualValues = new[] { "15-fd1654TU", "512 GB SSD" };

        var results = fields.Select((field, index) =>
            _comparisonService.Compare(
                field,
                new FieldObservation { Value = masterValues[index], Confidence = 0.95 },
                new FieldObservation { Value = actualValues[index], Confidence = 0.95 }))
            .ToList();

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(InspectionStatus.PASS, result.Status));
    }

    [Fact]
    public void BatteryCallCorrectionRequiresBatteryContextAndWattHourEvidence()
    {
        var battery = CreateField("BATTERY", "Battery");
        var model = CreateField("MODEL", "Model");

        Assert.Equal("3-cell, 41 Wh", FieldComparisonService.CleanOcrValue(battery, "3-call, 41 Wh"));
        Assert.Equal("3-cell, 41 Wh", FieldComparisonService.CleanOcrValue(battery, "3-cell. 41. Wh-"));
        Assert.Equal("3-call", FieldComparisonService.CleanOcrValue(model, "3-call"));
    }

    [Fact]
    public void ConfigSelectorReturnsOnlyTheTwoRequiredActiveFields()
    {
        var fields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No."),
            CreateField("PROCESSOR", "Processor"),
            CreateField("STORAGE", "Storage"),
            CreateField("RAM", "RAM")
        };
        fields[2].IsActive = false;

        var selected = ConfigComparisonFieldSelector.Select(fields);

        Assert.Equal(["MODEL_NO", "QUEUE_NO"], selected.Select(field => field.FieldName));
    }

    [Theory]
    [InlineData("MODEL_NO", "HP Laptop 15-fd1654TU", "hp  laptop 15 – fd1654tu", InspectionStatus.PASS)]
    [InlineData("QUEUE_NO", "E52PWPA#ACJ", "E52PWPA # ACJ", InspectionStatus.PASS)]
    [InlineData("QUEUE_NO", "e52pwpa#acj", "E52PWPA#ACJ", InspectionStatus.PASS)]
    [InlineData("MODEL_NO", "HP Laptop 15-fd1654TU", "HP Laptop 15-fd1655TU", InspectionStatus.FAIL)]
    [InlineData("QUEUE_NO", "E52PWPA#ACJ", "E52PWPA#ACE", InspectionStatus.FAIL)]
    [InlineData("QUEUE_NO", "E52PWPA#ACJ", "E52PWPAACJ", InspectionStatus.FAIL)]
    public void ConfigFieldsUseConservativeCaseAndWhitespaceNormalization(
        string fieldName,
        string master,
        string actual,
        InspectionStatus expected)
    {
        var result = _comparisonService.Compare(
            CreateField(fieldName, fieldName),
            new FieldObservation { Value = master, Confidence = 0.95 },
            new FieldObservation { Value = actual, Confidence = 0.95 });

        Assert.Equal(expected, result.Status);
    }

    [Theory]
    [InlineData("MODEL_NO")]
    [InlineData("QUEUE_NO")]
    public void MissingConfigFieldRequiresReview(string fieldName)
    {
        var result = _comparisonService.Compare(
            CreateField(fieldName, fieldName),
            new FieldObservation { Value = fieldName == "MODEL_NO" ? "HP Laptop 15-fd1654TU" : "E52PWPA#ACJ", Confidence = 0.95 },
            null);

        Assert.Equal(InspectionStatus.REVIEW_REQUIRED, result.Status);
    }

    [Fact]
    public void ConfigSelectorIgnoresSpecsBarcodeAndAnnotationFields()
    {
        var fields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No."),
            CreateField("PROCESSOR", "Processor"),
            CreateField("RAM", "RAM"),
            CreateField("STORAGE", "Storage"),
            CreateField("BARCODE", "Barcode"),
            CreateField("ANNOTATION", "Handwritten annotation")
        };

        var selected = ConfigComparisonFieldSelector.Select(fields);
        var comparisons = selected.Select(field => _comparisonService.Compare(
            field,
            new FieldObservation
            {
                Value = field.FieldName == "MODEL_NO" ? "HP Laptop 15-fd1654TU" : "E52PWPA#ACJ",
                Confidence = 0.95
            },
            new FieldObservation
            {
                Value = field.FieldName == "MODEL_NO" ? "HP Laptop 15-fd1654TU" : "E52PWPA#ACJ",
                Confidence = 0.95
            })).ToList();

        Assert.Equal(2, selected.Count);
        Assert.Equal(2, comparisons.Count);
        Assert.All(comparisons, result => Assert.Equal(InspectionStatus.PASS, result.Status));
    }

    [Fact]
    public void ConfigSelectorRequiresBothConfiguredComparisonFields()
    {
        var exception = Assert.Throws<InspectionProcessingException>(() =>
            ConfigComparisonFieldSelector.Select([CreateField("MODEL_NO", "Model No.")]));

        Assert.Contains("MODEL_NO", exception.Message);
        Assert.Contains("QUEUE_NO", exception.Message);
    }

    private FieldComparisonAssessment Compare(string fieldName, string master, string actual) =>
        _comparisonService.Compare(
            CreateField(fieldName, fieldName),
            new FieldObservation { Value = master, Confidence = 0.95 },
            new FieldObservation { Value = actual, Confidence = 0.95 });

    private static FieldDefinition CreateField(string fieldName, string displayName) => new()
    {
        LabelProfileId = Guid.NewGuid(),
        FieldName = fieldName,
        DisplayName = displayName,
        Required = true,
        Critical = true,
        OcrConfidenceThreshold = 0.55
    };
}
