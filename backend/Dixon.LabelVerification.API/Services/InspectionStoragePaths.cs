namespace Dixon.LabelVerification.API.Services;

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
}
