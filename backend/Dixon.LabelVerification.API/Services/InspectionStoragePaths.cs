namespace Dixon.LabelVerification.API.Services;

using Dixon.LabelVerification.API.Entities;

internal static class InspectionStoragePaths
{
    public static string ResolveRoot(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredPath = configuration["Storage:InspectionDirectory"];
        var path = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(environment.ContentRootPath, "..", "..", "storage", "inspections")
            : Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(environment.ContentRootPath, configuredPath);
        return Path.GetFullPath(path);
    }

    public static string ResolvePath(
        string root,
        string relativePath,
        string invalidPathMessage = "The stored image path is invalid.")
    {
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InspectionValidationException(invalidPathMessage);
        return fullPath;
    }

    public static string MasterImagePath(string modelCode, LabelType labelType) =>
        Join("MasterImages", SanitizeSegment(modelCode), labelType == LabelType.CO ? "MasterCO.jpg" : "MasterConfig.jpg");

    public static string ActualImagePath(string modelCode, Guid inspectionId, InspectionImageType imageType) =>
        Join("ActualImages", SanitizeSegment(modelCode), inspectionId.ToString("N"),
            imageType == InspectionImageType.ACTUAL_CO ? "ActualCO.jpg" : "ActualConfig.jpg");

    private static string Join(params string[] segments) => string.Join('/', segments);

    private static string SanitizeSegment(string value)
    {
        var clean = new string(value.Trim().Where(char.IsLetterOrDigit).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "UnknownModel" : clean;
    }
}
