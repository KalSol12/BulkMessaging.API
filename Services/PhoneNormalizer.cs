namespace BulkMessaging.API.Services;

public static class PhoneNormalizer
{
    /// <summary>
    /// Strips spaces, dashes, parens, and leading country code noise.
    /// Keeps digits and a single leading '+'.
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim();
        var hasPlus = trimmed.StartsWith("+");
        var digits = new string(trimmed.Where(char.IsDigit).ToArray());

        return hasPlus ? "+" + digits : digits;
    }
}