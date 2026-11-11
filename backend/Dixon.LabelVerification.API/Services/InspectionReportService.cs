using System.Text.Json;
using System.Security.Cryptography;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionReportService(
    LabelVerificationDbContext db,
    IConfiguration configuration,
    IWebHostEnvironment environment,
    ILogger<InspectionReportService> logger) : IInspectionReportService
{
    private readonly string _storageRoot = ResolveStorageRoot(configuration, environment);

    public async Task<InspectionReportDownload> GenerateOrGetAsync(
        Guid inspectionId,
        Guid generatedByUserId,
        CancellationToken cancellationToken)
    {
        var inspection = await db.InspectionSessions.AsNoTracking()
            .Include(x => x.Brand)
            .Include(x => x.Model)
            .Include(x => x.CreatedByUser)
            .Include(x => x.Images)
            .Include(x => x.StageResults).ThenInclude(x => x.FieldComparisons)
            .Include(x => x.Images).ThenInclude(x => x.OcrResults)
            .SingleOrDefaultAsync(x => x.Id == inspectionId, cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");

        if (inspection.Status is InspectionStatus.READY or InspectionStatus.PROCESSING)
            throw new ConfigurationConflictException("A PDF report is available only after inspection processing completes.");

        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == generatedByUserId && x.IsActive, cancellationToken)
            ?? throw new InspectionValidationException("The authenticated report user is unavailable.");
        var reportNumber = $"RPT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        var relativePath = Path.Combine(inspectionId.ToString("N"), "reports", $"{Guid.NewGuid():N}.pdf");
        var fullPath = ResolveStoredPath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        try
        {
            var images = new List<ReportImage>();
            foreach (var image in inspection.Images.OrderBy(x => x.ImageType))
            {
                var imagePath = ResolveStoredPath(image.StoragePath);
                if (!File.Exists(imagePath))
                {
                    logger.LogWarning("Cannot include missing image {ImageId} in report for {InspectionId}", image.Id, inspectionId);
                    continue;
                }

                images.Add(new ReportImage(image.ImageType.ToString(), image.OriginalFileName,
                    await File.ReadAllBytesAsync(imagePath, cancellationToken)));
            }

            var extracted = inspection.Images
                .SelectMany(image => image.OcrResults.SelectMany(result =>
                    ReadOcrLines(result.ExtractedFieldsJson)
                    .Select(line => new ReportOcrLine(image.ImageType.ToString(), line.Text, line.Confidence))))
                .ToList();
            ConfigureQuestPdf();
            var document = new InspectionReportDocument(
                inspection, inspection.CreatedByUser.FullName, images, extracted);
            await Task.Run(() => document.GeneratePdf(fullPath), cancellationToken);
            await using var reportStream = File.OpenRead(fullPath);
            var reportHash = Convert.ToHexString(await SHA256.HashDataAsync(reportStream, cancellationToken));
            var report = new InspectionReport
            {
                InspectionSessionId = inspectionId,
                ReportNumber = reportNumber,
                ReportFilePath = relativePath,
                FileSizeBytes = reportStream.Length,
                Sha256Hash = reportHash,
                ContentType = "application/pdf",
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                GeneratedByUserId = generatedByUserId
            };
            db.InspectionReports.Add(report);
            await db.SaveChangesAsync(cancellationToken);
            return new InspectionReportDownload(
                new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true),
                $"{reportNumber}.pdf");
        }

        catch
        {
            if (File.Exists(fullPath))
                File.Delete(fullPath);
            throw;
        }
    }

    private static IReadOnlyList<OcrLineEvidence> ReadOcrLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var lines = root.ValueKind == JsonValueKind.Array
            ? root
            : root.TryGetProperty("lines", out var value) ? value : default;
        return lines.ValueKind == JsonValueKind.Array
            ? JsonSerializer.Deserialize<List<OcrLineEvidence>>(lines.GetRawText()) ?? []
            : [];
    }

    private void ConfigureQuestPdf()
    {
        QuestPDF.Settings.License = configuration["Reports:QuestPDFLicense"]?.Trim() switch
        {
            "Community" => LicenseType.Community,
            "Professional" => LicenseType.Professional,
            "Enterprise" => LicenseType.Enterprise,
            _ => throw new ConfigurationConflictException(
                "Set Reports:QuestPDFLicense to the license tier applicable to your organization.")
        };
    }

    private string ResolveStoredPath(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, relativePath));
        var prefix = _storageRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _storageRoot
            : _storageRoot + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InspectionValidationException("The stored file path is invalid.");
        return fullPath;
    }

    private static string ResolveStorageRoot(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configured = configuration["Storage:InspectionDirectory"];
        var path = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(environment.ContentRootPath, "..", "..", "storage", "inspections")
            : Path.IsPathRooted(configured) ? configured : Path.Combine(environment.ContentRootPath, configured);
        return Path.GetFullPath(path);
    }

    private sealed record ReportImage(string Type, string FileName, byte[] Bytes);
    private sealed record ReportOcrLine(string ImageType, string Text, double Confidence);

    private sealed class InspectionReportDocument(
        InspectionSession inspection,
        string operatorName,
        IReadOnlyList<ReportImage> images,
        IReadOnlyList<ReportOcrLine> ocrLines) : IDocument
    {
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(style => style.FontSize(9).FontColor("#243047"));
                page.Header().Column(column =>
                {
                    column.Item().Text("Dixon Label Verification System").FontSize(19).Bold().FontColor("#254db8");
                    column.Item().PaddingTop(4).Text($"Inspection {inspection.InspectionNumber} · {inspection.CreatedAtUtc:yyyy-MM-dd HH:mm} UTC");
                });
                page.Content().PaddingVertical(18).Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Text("Inspection summary").FontSize(13).Bold();
                    column.Item().Text(
                        $"{inspection.Brand.Name} · {inspection.Model.Code} — {inspection.Model.Name}\n" +
                        $"Operator: {operatorName}\nProduction order: {inspection.ProductionOrderNumber ?? "—"}\n" +
                        $"Work order: {inspection.WorkOrderNumber ?? "—"}\n" +
                        $"Processing duration: {(inspection.ProcessingDurationMs is null ? "—" : $"{inspection.ProcessingDurationMs} ms")}\n" +
                        $"Final: {inspection.Status} · {inspection.FinalRemarks ?? "No final remarks"}");

                    foreach (var stage in inspection.StageResults.OrderBy(x => x.StageType))
                    {
                        column.Item().Text($"{stage.StageType} — {stage.Status}").FontSize(12).Bold();
                        column.Item().Text(stage.Remarks ?? string.Empty);
                        if (stage.FieldComparisons.Count > 0)
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn(1.1f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(1.2f);
                                    columns.RelativeColumn(0.7f);
                                    columns.RelativeColumn(0.8f);
                                    columns.RelativeColumn(1.1f);
                                });
                                table.Header(header =>
                                {
                                    header.Cell().Element(HeaderCell).Text("Field");
                                    header.Cell().Element(HeaderCell).Text("Expected / Master");
                                    header.Cell().Element(HeaderCell).Text("Actual");
                                    header.Cell().Element(HeaderCell).Text("Result");
                                    header.Cell().Element(HeaderCell).Text("Confidence");
                                    header.Cell().Element(HeaderCell).Text("Reason");
                                });
                                foreach (var comparison in stage.FieldComparisons)
                                {
                                    table.Cell().Element(BodyCell).Text(comparison.FieldKey);
                                    table.Cell().Element(BodyCell).Text(comparison.MasterValue ?? "Unreadable");
                                    table.Cell().Element(BodyCell).Text(comparison.ActualValue ?? "Unreadable");
                                    table.Cell().Element(BodyCell).Text(comparison.Status.ToString());
                                    table.Cell().Element(BodyCell).Text(
                                        $"{(comparison.MasterConfidence is null ? "—" : comparison.MasterConfidence.Value.ToString("P0"))} / " +
                                        $"{(comparison.ActualConfidence is null ? "—" : comparison.ActualConfidence.Value.ToString("P0"))}");
                                    table.Cell().Element(BodyCell).Text(comparison.MismatchReason ?? "—");
                                }
                            });
                        }
                    }

                    column.Item().Text("OCR evidence").FontSize(12).Bold();
                    if (ocrLines.Count == 0)
                        column.Item().Text("No OCR lines were extracted.");
                    foreach (var line in ocrLines)
                        column.Item().Text($"{line.ImageType} · {line.Confidence:P0} · {line.Text}");

                    column.Item().Text("Original uploaded label images").FontSize(12).Bold();
                    foreach (var image in images)
                    {
                        column.Item().Text($"{image.Type} · {image.FileName}").SemiBold();
                        column.Item().Image(image.Bytes).FitArea();
                    }
                });
                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated ");
                    text.Span(DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'"));
                    text.Span(" · Dixon Label Verification");
                });
            });
        }

        private static IContainer HeaderCell(IContainer container) =>
            container.Background("#eaf0ff").Padding(5).DefaultTextStyle(style => style.Bold());

        private static IContainer BodyCell(IContainer container) =>
            container.BorderBottom(1).BorderColor("#e7ebf2").Padding(5);
    }
}
