using System.ComponentModel.DataAnnotations;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.DTOs;

public sealed class CreateInspectionRequest
{
    [Required]
    public Guid BrandId { get; init; }

    [Required]
    public Guid ModelId { get; init; }

    [StringLength(100)]
    public string? ProductionOrderNumber { get; init; }

    [StringLength(100)]
    public string? WorkOrderNumber { get; init; }
}

public sealed record InspectionImageResponse(
    Guid Id,
    InspectionImageType ImageType,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    int Width,
    int Height,
    string Sha256Hash,
    DateTimeOffset UploadedAtUtc,
    string ImageQualityStatus);

public sealed record InspectionSummaryResponse(
    Guid Id,
    string InspectionNumber,
    Guid BrandId,
    string BrandName,
    Guid ModelId,
    string ModelNumber,
    string ModelName,
    string? ProductionOrderNumber,
    string? WorkOrderNumber,
    InspectionStatus Status,
    InspectionStatus COStatus,
    InspectionStatus ConfigStatus,
    InspectionStatus SerialStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? InspectorName,
    string? InspectorEmployeeId);

public sealed record InspectionDetailsResponse(
    InspectionSummaryResponse Inspection,
    Guid? LabelProfileCOId,
    int? LabelProfileCOVersion,
    Guid? LabelProfileConfigId,
    int? LabelProfileConfigVersion,
    IReadOnlyList<InspectionImageResponse> Images,
    IReadOnlyList<InspectionOcrEvidenceResponse> OcrResults,
    IReadOnlyList<InspectionStageResponse> StageResults,
    IReadOnlyList<InspectionLogResponse> Logs,
    string? FinalRemarks);

public sealed record InspectionOcrEvidenceResponse(
    Guid Id,
    Guid ImageId,
    InspectionImageType ImageType,
    string Engine,
    string EngineVersion,
    InspectionStatus Status,
    string? RawExtractedText,
    string? ExtractedFieldsJson,
    double? OverallConfidence,
    long? ProcessingDurationMs,
    IReadOnlyList<ExtractedFieldResponse> ExtractedFields);

public sealed record ExtractedFieldResponse(
    Guid? FieldDefinitionId,
    string FieldKey,
    string? ExtractedValue,
    string? NormalizedValue,
    double Confidence,
    bool IsManuallyCorrected,
    string? CorrectionReason);

public sealed class ManualFieldCorrectionRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Value { get; init; } = string.Empty;

    [Required, StringLength(1000, MinimumLength = 5)]
    public string Reason { get; init; } = string.Empty;
}

public sealed record InspectionStageResponse(
    Guid Id,
    InspectionStageType StageType,
    InspectionStatus Status,
    int TotalFields,
    int MatchedFields,
    int MismatchedFields,
    int UnreadableFields,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Remarks,
    IReadOnlyList<FieldComparisonResponse> Comparisons);

public sealed record FieldComparisonResponse(
    string FieldKey,
    string? MasterValue,
    string? ActualValue,
    string? NormalizedMasterValue,
    string? NormalizedActualValue,
    FieldComparisonStrategy ComparisonStrategy,
    InspectionStatus Status,
    double? MasterConfidence,
    double? ActualConfidence,
    string? MismatchReason);

public sealed record InspectionLogResponse(
    Guid Id,
    Guid? UserId,
    string Action,
    string Description,
    string? OldValue,
    string? NewValue,
    string? IPAddress,
    DateTimeOffset CreatedAtUtc);

public sealed class InspectionListQuery
{
    [StringLength(100)]
    public string? Search { get; init; }

    public Guid? BrandId { get; init; }
    public Guid? ModelId { get; init; }
    public InspectionStatus? Status { get; init; }
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;
    [Range(1, 100)]
    public int PageSize { get; init; } = 25;
    [RegularExpression("^(createdAtUtc|inspectionNumber|status)$")]
    public string SortBy { get; init; } = "createdAtUtc";
    public bool Descending { get; init; } = true;
}

public sealed record PagedInspectionResponse(
    IReadOnlyList<InspectionSummaryResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record InspectionImageContent(
    Stream Content,
    string ContentType,
    string DownloadFileName);
