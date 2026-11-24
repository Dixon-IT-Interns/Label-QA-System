using System.Security.Cryptography;
using System.Text.Json;
using Dixon.LabelVerification.API.Data;
using Dixon.LabelVerification.API.Entities;
using Dixon.LabelVerification.API.Interfaces;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dixon.LabelVerification.API.Services;

public sealed class InspectionReportService(
    LabelVerificationDbContext db, IConfiguration configuration, IWebHostEnvironment environment,
    ILogger<InspectionReportService> logger, AuthenticatedUserContext currentUser,
    IActivityLogService activityLog) : IInspectionReportService
{
    private readonly string _storageRoot = InspectionStoragePaths.ResolveRoot(configuration, environment);

    public async Task<InspectionReportDownload> GenerateOrGetAsync(Guid inspectionId, Guid generatedByUserId,
        string role, Guid? brandId, CancellationToken cancellationToken)
    {
        if (generatedByUserId != currentUser.UserId || role != currentUser.Role || brandId != currentUser.BrandId)
            throw new UnauthorizedAccessException("The report request does not match the authenticated account.");
        var inspection = await InspectionQueryScope.Apply(db.InspectionSessions.AsNoTracking(), generatedByUserId, role, brandId)
            .Where(x => x.Id == inspectionId).Include(x => x.Brand).Include(x => x.Model).Include(x => x.Images).ThenInclude(x => x.OcrResults)
            .Include(x => x.StageResults).ThenInclude(x => x.FieldComparisons).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InspectionNotFoundException($"Inspection '{inspectionId}' was not found.");
        currentUser.EnsureCanAccessBrand(inspection.BrandId);
        if (inspection.Status is InspectionStatus.READY or InspectionStatus.PROCESSING)
            throw new ConfigurationConflictException("A PDF report is available only after inspection processing completes.");
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == generatedByUserId, cancellationToken)
            ?? throw new InspectionValidationException("The authenticated report user is unavailable.");
        var reportNumber = $"RPT-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32];
        var relativePath = Path.Combine(inspectionId.ToString("N"), "reports", $"{Guid.NewGuid():N}.pdf");
        var fullPath = InspectionStoragePaths.ResolvePath(_storageRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        try
        {
            var images = new List<ReportImage>();
            foreach (var image in inspection.Images.OrderBy(x => x.ImageType))
            {
                var path = InspectionStoragePaths.ResolvePath(_storageRoot, image.StoragePath);
                if (File.Exists(path)) images.Add(new ReportImage(image.ImageType.ToString(), image.OriginalFileName, await File.ReadAllBytesAsync(path, cancellationToken)));
                else logger.LogWarning("Cannot include missing image {ImageId} in report for {InspectionId}", image.Id, inspectionId);
            }
            var ocrLines = inspection.Images.SelectMany(image => image.OcrResults.SelectMany(result => ReadOcrLines(result.ExtractedFieldsJson).Select(line => new ReportOcrLine(image.ImageType.ToString(), line.Text, line.Confidence)))).ToList();
            ConfigureQuestPdf();
            new InspectionReportDocument(inspection, user.Username, user.FullName, images, ocrLines).GeneratePdf(fullPath);
            await using var reportStream = File.OpenRead(fullPath);
            var reportHash = Convert.ToHexString(await SHA256.HashDataAsync(reportStream, cancellationToken));
            db.InspectionReports.Add(new InspectionReport { InspectionSessionId = inspectionId, ReportNumber = reportNumber, ReportFilePath = relativePath, FileSizeBytes = reportStream.Length, Sha256Hash = reportHash, GeneratedByUserId = generatedByUserId });
            await activityLog.RecordAsync("PDF_GENERATED", $"Generated report {reportNumber}.", inspection.BrandId, inspectionId, entityType: "InspectionReport", cancellationToken: cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return new InspectionReportDownload(new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true), $"{reportNumber}.pdf");
        }
        catch { if (File.Exists(fullPath)) File.Delete(fullPath); throw; }
    }

    private static IReadOnlyList<OcrLineEvidence> ReadOcrLines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        var lines = root.ValueKind == JsonValueKind.Array ? root : root.TryGetProperty("lines", out var value) ? value : default;
        return lines.ValueKind == JsonValueKind.Array ? JsonSerializer.Deserialize<List<OcrLineEvidence>>(lines.GetRawText()) ?? [] : [];
    }
    private void ConfigureQuestPdf() => QuestPDF.Settings.License = configuration["Reports:QuestPDFLicense"]?.Trim() switch { "Professional" => LicenseType.Professional, "Enterprise" => LicenseType.Enterprise, _ => LicenseType.Community };
    private sealed record ReportImage(string Type, string FileName, byte[] Bytes);
    private sealed record ReportOcrLine(string ImageType, string Text, double Confidence);

    private sealed class InspectionReportDocument(InspectionSession inspection, string employeeId, string employeeName, IReadOnlyList<ReportImage> images, IReadOnlyList<ReportOcrLine> ocrLines) : IDocument
    {
        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4); page.Margin(28); page.DefaultTextStyle(style => style.FontSize(9).FontColor("#20252d"));
                page.Header().Background("#173b70").Padding(12).Row(row => { row.RelativeItem().Text("LABEL LENS").FontSize(18).Bold().FontColor(Colors.White); row.AutoItem().Text("INSPECTION REPORT").FontSize(10).Bold().FontColor(Colors.White); });
                page.Content().PaddingVertical(14).Column(column =>
                {
                    column.Spacing(10); Section(column, "REPORT DETAILS");
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns => { columns.RelativeColumn(); columns.RelativeColumn(); });
                        Detail(table, "Report No.", inspection.InspectionNumber); Detail(table, "Employee ID", employeeId); Detail(table, "Employee", employeeName); Detail(table, "Date / Time", inspection.StartedAtUtc.ToLocalTime().ToString("dd-MM-yyyy HH:mm")); Detail(table, "Master Model / SKU", $"{inspection.Model.Name} / {inspection.Model.Code}"); Detail(table, "Brand", inspection.Brand.Name); Detail(table, "Production Order", inspection.ProductionOrderNumber ?? "—"); Detail(table, "Work Order", inspection.WorkOrderNumber ?? "—");
                    });
                    Section(column, "INSPECTION RESULT");
                    column.Item().Row(row => { Result(row, "OVERALL", inspection.Status); Result(row, "COO", inspection.COStatus); Result(row, "CONFIG", inspection.ConfigStatus); Result(row, "SERIAL", inspection.SerialStatus); }); column.Item().Text(inspection.FinalRemarks ?? "No final remarks.");
                    column.Item().PageBreak(); Section(column, "COMPARISON RESULTS");
                    foreach (var stage in inspection.StageResults.OrderBy(x => x.StageType))
                    {
                        column.Item().Text(stage.StageType == InspectionStageType.CO_COMPARISON ? "COO RESULT" : stage.StageType == InspectionStageType.CONFIG_COMPARISON ? "CONFIG RESULT" : "SERIAL VALIDATION").FontSize(11).Bold(); column.Item().Text($"Result: {stage.Status}   {stage.Remarks ?? string.Empty}");
                        if (stage.FieldComparisons.Count == 0) continue;
                        column.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns => { columns.RelativeColumn(1.1f); columns.RelativeColumn(1.25f); columns.RelativeColumn(1.25f); columns.RelativeColumn(.75f); columns.RelativeColumn(1.3f); });
                            table.Header(header => { header.Cell().Element(HeaderCell).Text("Field"); header.Cell().Element(HeaderCell).Text("Master"); header.Cell().Element(HeaderCell).Text("Actual"); header.Cell().Element(HeaderCell).Text("Result"); header.Cell().Element(HeaderCell).Text("Remarks"); });
                            foreach (var comparison in stage.FieldComparisons) { table.Cell().Element(BodyCell).Text(comparison.FieldKey); table.Cell().Element(BodyCell).Text(comparison.MasterValue ?? "Unreadable"); table.Cell().Element(BodyCell).Text(comparison.ActualValue ?? "Unreadable"); table.Cell().Element(BodyCell).Text(comparison.Status.ToString()); table.Cell().Element(BodyCell).Text(comparison.MismatchReason ?? "—"); }
                        });
                    }
                    column.Item().PageBreak(); Section(column, "MASTER IMAGE VS LIVE IMAGE"); AddImagePair(column, images, InspectionImageType.MASTER_CO, InspectionImageType.ACTUAL_CO, "COO"); AddImagePair(column, images, InspectionImageType.MASTER_CONFIG, InspectionImageType.ACTUAL_CONFIG, "CONFIG");
                    column.Item().PageBreak(); Section(column, "REPORT EVIDENCE"); if (ocrLines.Count == 0) column.Item().Text("No OCR lines were extracted."); foreach (var line in ocrLines) column.Item().Text($"{line.ImageType} · {line.Confidence:P0} · {line.Text}");
                });
                page.Footer().AlignCenter().Text($"Generated {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm 'UTC'} · Dixon Label Verification");
            });
        }
        private static void Section(ColumnDescriptor column, string title) => column.Item().Text(title).FontSize(12).Bold().FontColor("#173b70");
        private static void Detail(TableDescriptor table, string label, string value) { table.Cell().Element(BodyCell).Text(label).Bold(); table.Cell().Element(BodyCell).Text(value); }
        private static void Result(RowDescriptor row, string label, InspectionStatus status) => row.RelativeItem().PaddingRight(5).Background("#eef3f8").Padding(7).Column(column => { column.Item().Text(label).FontSize(7).Bold().FontColor("#526273"); column.Item().Text(status.ToString()).FontSize(10).Bold(); });
        private static void AddImagePair(ColumnDescriptor column, IReadOnlyList<ReportImage> images, InspectionImageType masterType, InspectionImageType actualType, string label) { var master = images.SingleOrDefault(image => image.Type == masterType.ToString()); var actual = images.SingleOrDefault(image => image.Type == actualType.ToString()); column.Item().Text(label).FontSize(10).Bold(); column.Item().Row(row => { Image(row, "MASTER IMAGE", master); Image(row, "LIVE IMAGE", actual); }); }
        private static void Image(RowDescriptor row, string title, ReportImage? image) => row.RelativeItem().Padding(5).Column(column => { column.Item().Text(title).FontSize(8).Bold(); if (image is null) column.Item().Text("Image unavailable"); else column.Item().Height(190).Image(image.Bytes).FitArea(); });
        private static IContainer HeaderCell(IContainer container) => container.Background("#dce8f5").Padding(5).DefaultTextStyle(style => style.Bold());
        private static IContainer BodyCell(IContainer container) => container.BorderBottom(1).BorderColor("#d9e0e8").Padding(5);
    }
}
