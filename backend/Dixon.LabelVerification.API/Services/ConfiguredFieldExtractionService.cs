using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using Dixon.LabelVerification.API.DTOs;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class ConfiguredFieldExtractionService
{
    public static ConfiguredFieldExtraction Extract(
        FieldDefinition definition,
        IReadOnlyList<OcrLineEvidence> lines,
        IReadOnlyList<OcrFieldCandidate>? candidates = null)
    {
        var configuredCandidates = candidates?
            .Where(candidate => string.Equals(
                candidate.FieldName, definition.FieldName, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToArray();
        if (configuredCandidates is { Length: > 1 })
            return ConfiguredFieldExtraction.Unreadable("The OCR service returned duplicate candidates for this field.");

        if (configuredCandidates is { Length: 1 } && configuredCandidates[0] is { } configuredCandidate)
        {
            if (string.Equals(configuredCandidate.Status, "DETECTED", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(configuredCandidate.Value)
                && double.IsFinite(configuredCandidate.Confidence)
                && configuredCandidate.Confidence is >= 0 and <= 1)
            {
                return new ConfiguredFieldExtraction(
                    new FieldObservation
                    {
                        Value = FieldComparisonService.CleanOcrValue(definition, configuredCandidate.Value),
                        Confidence = configuredCandidate.Confidence
                    },
                    configuredCandidate.Box is null
                        ? null
                        : System.Text.Json.JsonSerializer.Serialize(configuredCandidate.Box),
                    configuredCandidate.Reason ?? "The configured OCR field candidate was detected.");
            }

            return ConfiguredFieldExtraction.Unreadable(configuredCandidate.Reason
                ?? (configuredCandidate.Status == "AMBIGUOUS"
                    ? "Multiple or spatially ambiguous OCR values matched this field."
                    : "No configured field candidate was detected."));
        }

        var labels = definition.Aliases
            .Where(alias => alias.IsActive)
            .Select(alias => alias.Value)
            .Append(definition.FieldName)
            .Append(definition.DisplayName)
            .Select(Normalize)
            .Where(label => label.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var matches = new List<(string Value, OcrLineEvidence Line)>();
        var labelSeenWithoutValue = false;
        foreach (var line in lines)
        {
            var normalizedLine = Normalize(line.Text);
            foreach (var label in labels)
            {
                var match = ValuePattern(label).Match(normalizedLine);
                if (match.Success)
                {
                    var value = match.Groups["value"].Value.Trim();
                    if (value.Length > 2000)
                        return ConfiguredFieldExtraction.Unreadable("The OCR value exceeds the supported field length.");
                    if (value.Length > 0)
                        matches.Add((value, line));
                }
                else if (string.Equals(normalizedLine, label, StringComparison.OrdinalIgnoreCase))
                {
                    labelSeenWithoutValue = true;
                }
            }
        }

        if (matches.Count > 1)
            return ConfiguredFieldExtraction.Unreadable("Multiple OCR lines matched this configured field.");
        if (matches.Count == 0)
        {
            return ConfiguredFieldExtraction.Unreadable(labelSeenWithoutValue
                ? "The configured label was detected without a delimited value."
                : "No configured field label with a delimited value was detected.");
        }

        var candidate = matches[0];
        return new ConfiguredFieldExtraction(
            new FieldObservation
            {
                Value = FieldComparisonService.CleanOcrValue(definition, candidate.Value),
                Confidence = candidate.Line.Confidence
            },
            candidate.Line.Box is null ? null : System.Text.Json.JsonSerializer.Serialize(candidate.Line.Box),
            "A configured label and delimited value were detected on one OCR line.");
    }

    private static string Normalize(string value) =>
        string.Join(' ', value.Normalize(NormalizationForm.FormKC)
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static Regex ValuePattern(string label) =>
        new($"^{Regex.Escape(label)}\\s*[:=]\\s*(?<value>.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
}

public sealed record OcrLineEvidence(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("box")] List<List<double>>? Box);

public sealed record ConfiguredFieldExtraction(
    FieldObservation? Observation,
    string? BoundingBoxJson,
    string Reason)
{
    public static ConfiguredFieldExtraction Unreadable(string reason) => new(null, null, reason);
}
