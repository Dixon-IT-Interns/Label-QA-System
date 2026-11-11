using System.Text.Json;
using System.Text.Json.Serialization;
using Dixon.LabelVerification.API.Entities;

namespace Dixon.LabelVerification.API.Services;

public static class InspectionComparisonArtifactWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<string> WriteAsync(
        string inspectionStorageRoot,
        Guid inspectionId,
        InspectionStatus overallStatus,
        IEnumerable<FieldComparison> comparisons,
        CancellationToken cancellationToken)
    {
        var resultDirectory = Path.GetFullPath(Path.Combine(
            inspectionStorageRoot, "..", "results", inspectionId.ToString("N")));
        Directory.CreateDirectory(resultDirectory);

        var resultPath = Path.Combine(resultDirectory, "comparison.json");
        var temporaryPath = Path.Combine(resultDirectory, $"comparison-{Guid.NewGuid():N}.tmp");
        var result = new
        {
            status = "completed",
            overallResult = ToResultLabel(overallStatus),
            inspectionId,
            fields = comparisons.Select(comparison => new
            {
                field = comparison.FieldKey,
                master = comparison.MasterValue,
                actual = comparison.ActualValue,
                masterNormalized = comparison.NormalizedMasterValue,
                actualNormalized = comparison.NormalizedActualValue,
                result = ToResultLabel(comparison.Status)
            })
        };

        try
        {
            await using (var stream = new FileStream(
                temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(stream, result, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            File.Move(temporaryPath, resultPath, true);
            return resultPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string ToResultLabel(InspectionStatus status) => status switch
    {
        InspectionStatus.PASS => "PASS",
        InspectionStatus.FAIL => "FAIL",
        InspectionStatus.NOT_APPLICABLE => "NOT_APPLICABLE",
        _ => "REVIEW"
    };
}
