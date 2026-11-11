    using System.Globalization;
using System.Text;
    using System.Text.RegularExpressions;
    using Dixon.LabelVerification.API.DTOs;
    using Dixon.LabelVerification.API.Entities;
    using Dixon.LabelVerification.API.Interfaces;

namespace Dixon.LabelVerification.API.Services;

public sealed class FieldComparisonService : IFieldComparisonService
{
    private static readonly Regex LeadingOcrNoise = new(@"^[\s•●▪◦·@©¢\\|]+", RegexOptions.Compiled);
    private static readonly Regex LaptopModel = new(
        @"\b(?<model>\d{2}-[A-Z]{2}\d{4,}[A-Z0-9]{0,8})\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex Capacity = new(
        @"(?<amount>\d+(?:\.\d+)?)\s*(?<unit>GB|TB)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex WindowsVersion = new(
        @"\bWindows\s*(?<version>\d{1,2})(?:\s+(?<edition>Home|Pro|Enterprise|Education))?\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string CleanOcrValue(FieldDefinition definition, string value)
    {
        var productField = GetProductField(definition);
        if (productField == ProductField.None)
            return value;

        var normalized = value
            .Replace("®", string.Empty, StringComparison.Ordinal)
            .Replace("™", string.Empty, StringComparison.Ordinal)
            .Normalize(NormalizationForm.FormKC);
        normalized = LeadingOcrNoise.Replace(normalized, string.Empty);
        if (productField == ProductField.Battery
            && Regex.IsMatch(normalized, @"\bWh\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            normalized = Regex.Replace(
                normalized,
                @"\b(?<count>\d+)\s*-\s*(?:cell|call|cel1)\s*[,.:]?\s*",
                "${count}-cell, ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            normalized = Regex.Replace(
                normalized,
                @"(?<=\d)\s*[.,]\s*(?=Wh\b)",
                " ",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            normalized = Regex.Replace(
                normalized,
                @"\bWh\b[-.:,]+",
                "Wh",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        return normalized;
    }

    public static string? NormalizeForStorage(FieldDefinition definition, string value)
    {
        var cleaned = CleanOcrValue(definition, value);
        var configField = GetConfigField(definition);
        if (configField != ConfigField.None)
            return NormalizeConfigField(configField, cleaned);

        var productField = GetProductField(definition);
        if (productField != ProductField.None)
            return NormalizeProductField(productField, cleaned);

        return definition.ComparisonStrategy switch
        {
            FieldComparisonStrategy.Exact => cleaned,
            FieldComparisonStrategy.CaseInsensitive => NormalizeDashes(cleaned).ToUpperInvariant(),
            FieldComparisonStrategy.NormalizedText => NormalizeText(cleaned),
            FieldComparisonStrategy.Identifier => NormalizeIdentifier(cleaned),
            FieldComparisonStrategy.MacAddress => NormalizeMacAddress(cleaned),
            FieldComparisonStrategy.Numeric => decimal.TryParse(
                cleaned,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var number)
                    ? number.ToString(CultureInfo.InvariantCulture)
                    : null,
            _ => null
        };
    }

    public FieldComparisonAssessment Compare(
        FieldDefinition definition,
        FieldObservation? master,
        FieldObservation? actual)
    {
        var masterAvailable = IsReadable(master, definition.OcrConfidenceThreshold);
        var actualAvailable = IsReadable(actual, definition.OcrConfidenceThreshold);

        if (!masterAvailable && !actualAvailable && !definition.Required)
        {
            return new FieldComparisonAssessment(
                InspectionStatus.NOT_APPLICABLE,
                "The optional field was not readable on either label.");
        }

        if (!masterAvailable || !actualAvailable)
        {
            return new FieldComparisonAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "A value is missing or below the configured OCR confidence threshold.");
        }

        var matches = CompareReadableValues(definition, master!.Value!, actual!.Value!);
        if (!matches.HasValue)
        {
            return new FieldComparisonAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "A value could not be interpreted using the configured comparison strategy.");
        }

        if (!matches.Value && !definition.Critical)
        {
            return new FieldComparisonAssessment(
                InspectionStatus.REVIEW_REQUIRED,
                "A non-critical field does not match and requires review.");
        }

        return matches.Value
            ? new FieldComparisonAssessment(InspectionStatus.PASS, "Values match.")
            : new FieldComparisonAssessment(InspectionStatus.FAIL, "Values do not match.");
    }

    private static bool? CompareReadableValues(FieldDefinition definition, string master, string actual)
    {
        var configField = GetConfigField(definition);
        if (configField != ConfigField.None)
        {
            return string.Equals(
                NormalizeConfigField(configField, CleanOcrValue(definition, master)),
                NormalizeConfigField(configField, CleanOcrValue(definition, actual)),
                StringComparison.Ordinal);
        }

        var productField = GetProductField(definition);
        return productField != ProductField.None
            ? string.Equals(
                NormalizeProductField(productField, CleanOcrValue(definition, master)),
                NormalizeProductField(productField, CleanOcrValue(definition, actual)),
                StringComparison.Ordinal)
            : definition.ComparisonStrategy switch
            {
                FieldComparisonStrategy.Exact =>
                    string.Equals(
                        CleanOcrValue(definition, master),
                        CleanOcrValue(definition, actual),
                        StringComparison.Ordinal),
                FieldComparisonStrategy.CaseInsensitive =>
                    string.Equals(
                        NormalizeDashes(CleanOcrValue(definition, master)),
                        NormalizeDashes(CleanOcrValue(definition, actual)),
                        StringComparison.OrdinalIgnoreCase),
                FieldComparisonStrategy.NormalizedText => string.Equals(
                    NormalizeText(CleanOcrValue(definition, master)),
                    NormalizeText(CleanOcrValue(definition, actual)),
                    StringComparison.Ordinal),
                FieldComparisonStrategy.Identifier => CompareNormalized(
                    NormalizeIdentifier(CleanOcrValue(definition, master)),
                    NormalizeIdentifier(CleanOcrValue(definition, actual))),
                FieldComparisonStrategy.MacAddress => CompareNormalized(
                    NormalizeMacAddress(CleanOcrValue(definition, master)),
                    NormalizeMacAddress(CleanOcrValue(definition, actual))),
                FieldComparisonStrategy.Numeric => CompareNumeric(
                    definition,
                    CleanOcrValue(definition, master),
                    CleanOcrValue(definition, actual)),
                _ => null
            };
    }

    private static string NormalizeText(string value)
    {
        var normalized = NormalizeDashes(value.Normalize(NormalizationForm.FormKC)).Trim();
        return string.Join(' ', normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeDashes(string value) =>
        value.Replace('\u2010', '-')
            .Replace('\u2011', '-')
            .Replace('\u2012', '-')
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-')
            .Replace('\uFE63', '-')
            .Replace('\uFF0D', '-');

    private static ProductField GetProductField(FieldDefinition definition)
    {
        var names = definition.Aliases
            .Where(alias => alias.IsActive)
            .Select(alias => alias.Value)
            .Prepend(definition.DisplayName)
            .Prepend(definition.FieldName)
            .Select(name => new string(name.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray()))
            .ToArray();

        if (names.Any(name => name.Contains("model", StringComparison.Ordinal)))
            return ProductField.Model;
        if (names.Any(name => name.Contains("processor", StringComparison.Ordinal) || name == "cpu"))
            return ProductField.Processor;
        if (names.Any(name => name.Contains("storage", StringComparison.Ordinal)
            || name is "ssd" or "harddrive"))
            return ProductField.Storage;
        if (names.Any(name => name.Contains("ram", StringComparison.Ordinal)
            || name.Contains("memory", StringComparison.Ordinal)))
            return ProductField.Memory;
        if (names.Any(name => name.Contains("operatingsystem", StringComparison.Ordinal)
            || name == "os" || name.StartsWith("os", StringComparison.Ordinal)))
            return ProductField.OperatingSystem;
        if (names.Any(name => name.Contains("display", StringComparison.Ordinal)
            || name.Contains("screen", StringComparison.Ordinal)))
            return ProductField.Display;
        if (names.Any(name => name.Contains("battery", StringComparison.Ordinal)))
            return ProductField.Battery;
        return ProductField.None;
    }

    private static ConfigField GetConfigField(FieldDefinition definition) =>
        definition.FieldName.ToUpperInvariant() switch
        {
            "MODEL_NO" => ConfigField.ModelNo,
            "QUEUE_NO" => ConfigField.QueueNo,
            _ => ConfigField.None
        };

    private static string NormalizeConfigField(ConfigField field, string value)
    {
        var normalized = NormalizeDashes(value.Normalize(NormalizationForm.FormKC)).Trim();
        return field switch
        {
            ConfigField.ModelNo => NormalizeModelNo(normalized),
            ConfigField.QueueNo => new string(normalized.Where(character => !char.IsWhiteSpace(character))
                .Select(char.ToUpperInvariant)
                .ToArray()),
            _ => NormalizeText(normalized)
        };
    }

    private static string NormalizeModelNo(string value)
    {
        var normalized = NormalizeText(Regex.Replace(
            value,
            @"(?<=\d)\s*-\s*(?=[A-Z]{2}\d)",
            "-",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
        var modelMatch = Regex.Match(
            normalized,
            @"\bHP\s+Laptop\s+(?<model>\d{2}-[A-Z]{2}\d{4,}[A-Z0-9]{0,8})\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return modelMatch.Success
            ? $"HP Laptop {modelMatch.Groups["model"].Value}".ToUpperInvariant()
            : normalized.ToUpperInvariant();
    }

    private static string NormalizeProductField(ProductField field, string value)
    {
        var normalized = NormalizeDashes(value).ToLowerInvariant();
        return field switch
        {
            ProductField.Model => NormalizeModel(normalized),
            ProductField.Processor => NormalizeText(
                Regex.Replace(normalized, @"\bprocessor\b", string.Empty, RegexOptions.CultureInvariant)),
            ProductField.Storage => NormalizeStorage(normalized),
            ProductField.Memory => NormalizeText(Capacity.Replace(normalized, match =>
                $"{match.Groups["amount"].Value}{match.Groups["unit"].Value.ToLowerInvariant()}")),
            ProductField.OperatingSystem => NormalizeOperatingSystem(normalized),
            ProductField.Display => NormalizeDisplay(normalized),
            ProductField.Battery => NormalizeBattery(normalized),
            _ => NormalizeText(normalized)
        };
    }

    private static string NormalizeModel(string value)
    {
        var match = LaptopModel.Match(value);
        if (match.Success)
            return match.Groups["model"].Value.ToLowerInvariant();

        var withoutBrand = Regex.Replace(value, @"\bhp\s+laptop\s+", string.Empty, RegexOptions.CultureInvariant);
        return NormalizeIdentifier(withoutBrand)?.ToLowerInvariant() ?? NormalizeText(withoutBrand);
    }

    private static string NormalizeStorage(string value)
    {
        var capacity = Capacity.Match(value);
        if (!capacity.Success)
            return NormalizeText(value);

        var amount = decimal.TryParse(
            capacity.Groups["amount"].Value,
            NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out var parsedAmount)
                ? parsedAmount.ToString(CultureInfo.InvariantCulture)
                : capacity.Groups["amount"].Value;
        var unit = capacity.Groups["unit"].Value.ToLowerInvariant();
        var storageType = Regex.IsMatch(value, @"\bnvme\b", RegexOptions.CultureInvariant)
            ? "nvme solid state drive"
            : Regex.IsMatch(value, @"\b(?:ssd|solid\s+state\s+drive)\b", RegexOptions.CultureInvariant)
                ? "solid state drive"
                : Regex.IsMatch(value, @"\b(?:hdd|hard\s+disk(?:\s+drive)?)\b", RegexOptions.CultureInvariant)
                    ? "hard disk drive"
                    : Regex.IsMatch(value, @"\b(?:emmc)\b", RegexOptions.CultureInvariant)
                        ? "emmc"
                        : null;
        return storageType is null
            ? NormalizeText(value)
            : $"{amount}{unit} {storageType}";
    }

    private static string NormalizeOperatingSystem(string value)
    {
        var match = WindowsVersion.Match(value);
        return match.Success
            ? NormalizeText($"windows {match.Groups["version"].Value} {match.Groups["edition"].Value}")
            : NormalizeText(value);
    }

    private static string NormalizeDisplay(string value)
    {
        var withoutDisplayLabel = Regex.Replace(value, @"\bdisplay\b", string.Empty, RegexOptions.CultureInvariant);
        withoutDisplayLabel = Regex.Replace(withoutDisplayLabel, @"(?<=\d)\s*(?:\""|inches?\b)", string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return NormalizeText(withoutDisplayLabel
            .Replace("(", " ", StringComparison.Ordinal)
            .Replace(")", " ", StringComparison.Ordinal)
            .Replace(",", " ", StringComparison.Ordinal));
    }

    private static string NormalizeBattery(string value)
    {
        var corrected = Regex.Replace(
            value,
            @"\b(?<count>\d+)\s*-\s*call\b(?=.*\bwh\b)",
            "${count}-cell",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return NormalizeText(corrected.Replace(",", " ", StringComparison.Ordinal));
    }

    private static string? NormalizeIdentifier(string value)
    {
        var normalized = new string(value.Normalize(NormalizationForm.FormKC)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        return normalized.Length == 0 ? null : normalized;
    }

    private static string? NormalizeMacAddress(string value)
    {
        var normalized = new string(value
            .Where(character => character is not (':' or '-' or '.') && !char.IsWhiteSpace(character))
            .ToArray());
        return normalized.Length == 12 && normalized.All(Uri.IsHexDigit)
            ? normalized.ToUpperInvariant()
            : null;
    }

    private static bool? CompareNormalized(string? master, string? actual) =>
        master is null || actual is null
            ? null
            : string.Equals(master, actual, StringComparison.Ordinal);

    private static bool? CompareNumeric(FieldDefinition definition, string master, string actual)
    {
        const NumberStyles numericSyntax = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (!decimal.TryParse(master, numericSyntax, CultureInfo.InvariantCulture, out var masterValue)
            || !decimal.TryParse(actual, numericSyntax, CultureInfo.InvariantCulture, out var actualValue))
        {
            return null;
        }

        try
        {
            var difference = Math.Abs(masterValue - actualValue);
            var scale = Math.Max(Math.Abs(masterValue), Math.Abs(actualValue));
            var allowedDifference = Math.Max(
                definition.NumericAbsoluteTolerance,
                scale * definition.NumericRelativeTolerance);
            return difference <= allowedDifference;
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static bool IsReadable(FieldObservation? observation, double minimumConfidence) =>
        observation is not null
        && !string.IsNullOrWhiteSpace(observation.Value)
        && observation.Confidence is >= 0 and <= 1
        && observation.Confidence >= minimumConfidence;

    private enum ProductField
    {
        None,
        Model,
        Processor,
        Storage,
        Memory,
        OperatingSystem,
        Display,
        Battery
    }

    private enum ConfigField
    {
        None,
        ModelNo,
        QueueNo
    }
}
