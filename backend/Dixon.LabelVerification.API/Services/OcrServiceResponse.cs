using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dixon.LabelVerification.API.Services;

public sealed record OcrServiceResponse(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("engine")] string Engine,
    [property: JsonPropertyName("engine_version")] string EngineVersion,
    [property: JsonPropertyName("image_quality")] OcrImageQuality ImageQuality,
    [property: JsonPropertyName("lines")] List<OcrLineEvidence> Lines,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("field_candidates")] List<OcrFieldCandidate>? FieldCandidates = null,
    [property: JsonPropertyName("warnings")] List<string>? Warnings = null,
    [property: JsonPropertyName("processing_duration_ms")] long ProcessingDurationMs = 0,
    [property: JsonPropertyName("raw_text")] string? RawText = null,
    [property: JsonPropertyName("ocr_confidence")] double? OcrConfidence = null,
    [property: JsonPropertyName("job_id")] string? JobId = null,
    [property: JsonPropertyName("artifacts")] OcrArtifactReferences? Artifacts = null);

public sealed record OcrArtifactReferences(
    [property: JsonPropertyName("original_image")] string OriginalImage,
    [property: JsonPropertyName("cleaned_image")] string CleanedImage,
    [property: JsonPropertyName("raw_ocr_text")] string RawOcrText,
    [property: JsonPropertyName("parsed_json")] string ParsedJson);
public sealed record OcrFieldCandidate(
    [property: JsonPropertyName("field_name")] string FieldName,
    [property: JsonPropertyName("value")] string? Value,
    [property: JsonPropertyName("confidence")] double Confidence,
    [property: JsonPropertyName("box")] List<List<double>>? Box,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string? Reason = null,
    [property: JsonPropertyName("debug")] JsonElement? Debug = null);

public sealed record OcrBarcodeResponse(
    [property: JsonPropertyName("barcodes")] List<OcrBarcodeEvidence> Barcodes,
    [property: JsonPropertyName("warnings")] List<string> Warnings,
    [property: JsonPropertyName("processing_duration_ms")] long ProcessingDurationMs);

public sealed record OcrBarcodeEvidence(
    [property: JsonPropertyName("format")] string Format,
    [property: JsonPropertyName("value")] string Value,
    [property: JsonPropertyName("box")] List<List<double>>? Box);

public sealed record OcrImageQuality(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("blur_score")] double BlurScore,
    [property: JsonPropertyName("brightness_mean")] double BrightnessMean = 0,
    [property: JsonPropertyName("contrast_stddev")] double ContrastStddev = 0);
