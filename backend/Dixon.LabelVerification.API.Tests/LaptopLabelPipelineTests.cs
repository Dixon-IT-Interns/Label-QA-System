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

    [Fact]
    public void CoSelectorReturnsOnlyModelQrAndPartNumberFields()
    {
        var fields = new[]
        {
            CreateField("SERIAL_NUMBER", "S/N"),
            CreateField("MODEL_NO", "Model No."),
            CreateField("QR_CODE", "QR Code"),
            CreateField("PART_NUMBER", "P/N"),
            CreateField("PROCESSOR", "Processor"),
            CreateField("WORK_ORDER", "Work Order")
        };

        var selected = CoComparisonFieldSelector.Select(fields);

        Assert.Equal(
            ["MODEL_NO", "QR_CODE", "PART_NUMBER"],
            selected.Select(field => field.FieldName));
    }

    [Fact]
    public void CoSelectorRequiresAllThreeFieldsAndIdentifierComparisonPreservesExactEvidence()
    {
        var exception = Assert.Throws<InspectionProcessingException>(() =>
            CoComparisonFieldSelector.Select([
                CreateField("PART_NUMBER", "P/N"),
                CreateField("MODEL_NO", "Model No.")
            ]));

        Assert.Contains("QR_CODE", exception.Message);
        var partNumber = CreateField("PART_NUMBER", "P/N");
        partNumber.ComparisonStrategy = FieldComparisonStrategy.Exact;
        var result = _comparisonService.Compare(
            partNumber,
            new FieldObservation { Value = "E52PWPA#ACJ", Confidence = 0.95 },
            new FieldObservation { Value = "E52PWPA#0CJ", Confidence = 0.95 });

        Assert.Equal(InspectionStatus.FAIL, result.Status);
    }

    [Fact]
    public void DefaultLabelProfilesUseTheSameCOAndConfigFieldKeys()
    {
        var model = new LaptopModel
        {
            BrandId = Guid.NewGuid(),
            Code = "MODEL-1",
            Name = "Model One"
        };

        var coProfile = DefaultLabelProfileProvisioner.CreateDefaultProfile(model, LabelType.CO, 1);
        var configProfile = DefaultLabelProfileProvisioner.CreateDefaultProfile(model, LabelType.CONFIG, 1);

        Assert.Equal(
            ["MODEL_NO", "QR_CODE", "PART_NUMBER"],
            coProfile.FieldDefinitions.Select(field => field.FieldName));
        Assert.Equal(
            ["MODEL_NO", "QUEUE_NO"],
            configProfile.FieldDefinitions.Select(field => field.FieldName));
        Assert.DoesNotContain(
            coProfile.FieldDefinitions,
            field => field.FieldName == "SERIAL_NUMBER");
    }

    [Theory]
    [InlineData("HP Laptop 15-fd1652TU", "15-FD1652TU")]
    [InlineData("Latitude 5540", "5540")]
    public void ModelCodeExtractorFindsModelCodeInOcrField(string value, string expected)
    {
        Assert.Equal(expected, ModelCodeExtractor.Extract(value));
    }

    [Theory]
    [InlineData("Model Number")]
    [InlineData("15-fd1652TU 15-fd1654TU")]
    public void ModelCodeExtractorDoesNotGuessWhenNoUniqueModelCodeExists(string value)
    {
        Assert.Null(ModelCodeExtractor.Extract(value));
    }

    [Fact]
    public void CrossLabelValidationComparesOnlyModelAndPartNumberToQueueNumber()
    {
        var coFields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QR_CODE", "QR Code"),
            CreateField("PART_NUMBER", "P/N")
        };
        var configFields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No.")
        };
        configFields[0].ComparisonStrategy = FieldComparisonStrategy.CaseInsensitive;
        configFields[1].ComparisonStrategy = FieldComparisonStrategy.CaseInsensitive;
        var coActual = new Dictionary<Guid, ConfiguredFieldExtraction>
        {
            [coFields[0].Id] = Extraction("HP Laptop 15-fd1652TU"),
            [coFields[2].Id] = Extraction("E52PWPA#ACJ")
        };
        var configActual = new Dictionary<Guid, ConfiguredFieldExtraction>
        {
            [configFields[0].Id] = Extraction("hp laptop 15-fd1652tu"),
            [configFields[1].Id] = Extraction("e52pwpa # acj")
        };

        var stage = CoConfigValidationService.Create(
            Guid.NewGuid(), coFields, configFields, coActual, configActual,
            DateTimeOffset.UtcNow, true, _comparisonService);

        Assert.Equal(InspectionStatus.PASS, stage.Status);
        Assert.Equal(2, stage.TotalFields);
        Assert.Equal(["MODEL_NO", "PART_NUMBER_TO_QUEUE_NO"],
            stage.FieldComparisons.Select(comparison => comparison.FieldKey));
        Assert.All(stage.FieldComparisons, comparison => Assert.Equal(InspectionStatus.PASS, comparison.Status));
        var partQueueComparison = stage.FieldComparisons.Single(comparison =>
            comparison.FieldKey == "PART_NUMBER_TO_QUEUE_NO");
        Assert.Equal("E52PWPA#ACJ", partQueueComparison.MasterValue);
        Assert.Equal("e52pwpa # acj", partQueueComparison.ActualValue);
    }

    [Fact]
    public void CrossLabelValidationFailsMismatchesAndReviewsMissingRequiredValues()
    {
        var coFields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QR_CODE", "QR Code"),
            CreateField("PART_NUMBER", "P/N")
        };
        var configFields = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No.")
        };
        var coActual = new Dictionary<Guid, ConfiguredFieldExtraction>
        {
            [coFields[0].Id] = Extraction("HP Laptop 15-fd1652TU"),
            [coFields[2].Id] = Extraction("E52PWPA#ACJ")
        };
        var configActual = new Dictionary<Guid, ConfiguredFieldExtraction>
        {
            [configFields[0].Id] = Extraction("HP Laptop 15-fd1653TU"),
            [configFields[1].Id] = Extraction("E52PWPA#ACE")
        };

        var mismatch = CoConfigValidationService.Create(
            Guid.NewGuid(), coFields, configFields, coActual, configActual,
            DateTimeOffset.UtcNow, true, _comparisonService);
        configActual.Remove(configFields[1].Id);
        var unreadable = CoConfigValidationService.Create(
            Guid.NewGuid(), coFields, configFields, coActual, configActual,
            DateTimeOffset.UtcNow, true, _comparisonService);

        Assert.Equal(InspectionStatus.FAIL, mismatch.Status);
        Assert.All(mismatch.FieldComparisons, comparison => Assert.Equal(InspectionStatus.FAIL, comparison.Status));
        Assert.Equal(InspectionStatus.FAIL, unreadable.Status);
        Assert.Equal(
            InspectionStatus.REVIEW_REQUIRED,
            unreadable.FieldComparisons.Single(comparison => comparison.FieldKey == "PART_NUMBER_TO_QUEUE_NO").Status);
    }

    [Fact]
    public void MasterLabelPairValidatorAcceptsMatchingModelAndPartQueueValues()
    {
        var coDefinitions = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QR_CODE", "QR Code"),
            CreateField("PART_NUMBER", "P/N")
        };
        var configDefinitions = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No.")
        };
        configDefinitions[0].ComparisonStrategy = FieldComparisonStrategy.CaseInsensitive;
        configDefinitions[1].ComparisonStrategy = FieldComparisonStrategy.CaseInsensitive;
        var coFields = new[]
        {
            MasterValue("MODEL_NO", "HP Laptop 15-fd1652TU"),
            MasterValue("PART_NUMBER", "E52PWPA#ACJ")
        };
        var configFields = new[]
        {
            MasterValue("MODEL_NO", "15-FD1652TU"),
            MasterValue("QUEUE_NO", "e52pwpa # acj")
        };

        MasterLabelPairValidator.Validate(
            coDefinitions, coFields, configDefinitions, configFields, _comparisonService);
    }

    [Fact]
    public void MasterLabelPairValidatorRejectsMismatchesAndUnreadableValues()
    {
        var coDefinitions = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("PART_NUMBER", "P/N")
        };
        var configDefinitions = new[]
        {
            CreateField("MODEL_NO", "Model No."),
            CreateField("QUEUE_NO", "Queue No.")
        };
        var coFields = new[]
        {
            MasterValue("MODEL_NO", "15-fd1652TU"),
            MasterValue("PART_NUMBER", "E52PWPA#ACJ")
        };
        var configFields = new[]
        {
            MasterValue("MODEL_NO", "15-fd1652TU"),
            MasterValue("QUEUE_NO", "E52PWPA#ACE")
        };

        Assert.Throws<InspectionConflictException>(() => MasterLabelPairValidator.Validate(
            coDefinitions, coFields, configDefinitions, configFields, _comparisonService));
        Assert.Throws<InspectionConflictException>(() => MasterLabelPairValidator.Validate(
            coDefinitions,
            [MasterValue("MODEL_NO", "15-fd1652TU"), MasterValue("PART_NUMBER", "E52PWPA#ACJ")],
            configDefinitions,
            [MasterValue("MODEL_NO", "15-fd1653TU"), MasterValue("QUEUE_NO", "E52PWPA#ACJ")],
            _comparisonService));
        Assert.Throws<InspectionValidationException>(() => MasterLabelPairValidator.Validate(
            coDefinitions,
            [MasterValue("MODEL_NO", "15-fd1652TU"), MasterValue("PART_NUMBER", "E52PWPA#ACJ", 0.1)],
            configDefinitions,
            [MasterValue("MODEL_NO", "15-fd1652TU"), MasterValue("QUEUE_NO", "E52PWPA#ACJ")],
            _comparisonService));
    }

    private FieldComparisonAssessment Compare(string fieldName, string master, string actual) =>
        _comparisonService.Compare(
            CreateField(fieldName, fieldName),
            new FieldObservation { Value = master, Confidence = 0.95 },
            new FieldObservation { Value = actual, Confidence = 0.95 });

    private static MasterLabelField MasterValue(string key, string value, double confidence = 0.95) => new()
    {
        FieldKey = key,
        DisplayName = key,
        ExtractedValue = value,
        Confidence = confidence,
        Reason = "Test master value."
    };

    private static ConfiguredFieldExtraction Extraction(string value) =>
        new(new FieldObservation { Value = value, Confidence = 0.95 }, null, "Test field value.");

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
