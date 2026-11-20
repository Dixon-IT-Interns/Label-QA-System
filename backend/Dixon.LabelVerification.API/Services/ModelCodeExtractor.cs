using System.Text.RegularExpressions;

namespace Dixon.LabelVerification.API.Services;

public static partial class ModelCodeExtractor
{
    private static readonly HashSet<string> ExcludedTokens = new(
        ["MODEL", "MODELNO", "MODELNUMBER", "PRODUCT", "LAPTOP", "NOTEBOOK", "NUMBER"],
        StringComparer.OrdinalIgnoreCase);

    public static string? Extract(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var candidates = ModelCodeTokenRegex().Matches(value)
            .Select(match => match.Value)
            .Where(token => !ExcludedTokens.Contains(token)
                && (token.Any(char.IsLetter) && token.Any(char.IsDigit)
                    || token.Length >= 4 && token.All(char.IsDigit)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(token => token.Length)
            .ToArray();

        if (candidates.Length == 0)
            return null;
        if (candidates.Length == 1
            || candidates[0].Length >= candidates[1].Length + 4)
            return candidates[0].ToUpperInvariant();
        return null;
    }

    [GeneratedRegex(@"(?<![A-Z0-9])[A-Z0-9][A-Z0-9._-]{2,99}(?![A-Z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex ModelCodeTokenRegex();
}
