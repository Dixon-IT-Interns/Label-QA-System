namespace Dixon.LabelVerification.API.Interfaces;

public interface IInspectionReportService
{
    Task<InspectionReportDownload> GenerateOrGetAsync(
        Guid inspectionId,
        Guid generatedByUserId,
        CancellationToken cancellationToken);
}

public sealed record InspectionReportDownload(Stream Content, string FileName);
