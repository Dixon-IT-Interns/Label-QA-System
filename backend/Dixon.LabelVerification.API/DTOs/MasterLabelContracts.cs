using System.ComponentModel.DataAnnotations;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs;

/// <summary>Uploads the COO and Config master labels as one validated pair.</summary>
public sealed record SaveMasterLabelPairRequest
{
    [Required]
    public Guid BrandId { get; init; }

    [Required]
    public Guid ModelId { get; init; }

    [Required]
    public required IFormFile CoFile { get; init; }

    [Required]
    public required IFormFile ConfigFile { get; init; }
}

/// <summary>Requests OCR-based model detection from a label image.</summary>
public sealed record DetectModelRequest
{
    [Required]
    public Guid BrandId { get; init; }

    [Required]
    public required IFormFile File { get; init; }
}

/// <summary>Indicates whether both required master label types are configured for a model.</summary>
public sealed record MasterLabelAvailabilityResponse(
    Guid ModelId,
    bool HasCoMaster,
    bool HasConfigMaster,
    string? Message);

/// <summary>Represents a model detected from an uploaded label image.</summary>
public sealed record DetectedModelResponse(
    Guid ModelId,
    Guid BrandId,
    string ModelCode,
    string ModelName,
    bool WasCreated);

/// <summary>Represents one configured field extracted from a master label.</summary>
public sealed record MasterLabelFieldResponse(
    string FieldKey,
    string DisplayName,
    string? ExtractedValue,
    string? NormalizedValue,
    double Confidence,
    string Reason);

/// <summary>Represents a saved master label and its OCR evidence.</summary>
public sealed record MasterLabelResponse(
    Guid Id,
    Guid BrandId,
    Guid ModelId,
    string BrandName,
    string ModelCode,
    string ModelName,
    LabelType LabelType,
    Guid LabelProfileId,
    int LabelProfileVersion,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256Hash,
    string Engine,
    string EngineVersion,
    InspectionStatus OcrStatus,
    string? RawExtractedText,
    double? OverallConfidence,
    string ImageQualityStatus,
    DateTimeOffset UploadedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    IReadOnlyList<MasterLabelFieldResponse> Fields);

/// <summary>Represents both master labels saved after cross-label validation.</summary>
public sealed record MasterLabelPairResponse(
    MasterLabelResponse Co,
    MasterLabelResponse Config);

/// <summary>Provides a private master image stream for an authorized supervisor.</summary>
public sealed record MasterLabelImageContent(Stream Content, string ContentType, string DownloadFileName);
