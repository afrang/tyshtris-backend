using System.Text.RegularExpressions;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Settings.Domain;

public static class TextDirections
{
    public const string Ltr = "ltr";
    public const string Rtl = "rtl";

    public static bool IsValid(string value) =>
        string.Equals(value, Ltr, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, Rtl, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Ltr;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (!IsValid(normalized))
        {
            throw new ArgumentException("direction must be 'ltr' or 'rtl'.", nameof(value));
        }

        return normalized;
    }
}

public sealed class Language : Entity
{
    private static readonly Regex PrefixPattern = new(
        @"^[a-z]{2}(-[a-z0-9]{2,8})?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Name { get; private set; } = string.Empty;
    public string Prefix { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }
    public string Direction { get; private set; } = TextDirections.Ltr;

    private Language()
    {
    }

    public static Language Create(string name, string prefix, bool isDefault, string? direction)
    {
        return new Language
        {
            Id = Guid.Empty,
            Name = NormalizeName(name),
            Prefix = NormalizePrefix(prefix),
            IsDefault = isDefault,
            Direction = TextDirections.Normalize(direction)
        };
    }

    public void Update(string name, string prefix, bool isDefault, string? direction)
    {
        Name = NormalizeName(name);
        Prefix = NormalizePrefix(prefix);
        IsDefault = isDefault;
        Direction = TextDirections.Normalize(direction);
    }

    public void SetDefault(bool isDefault) => IsDefault = isDefault;

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("name is required.", nameof(name));
        }

        return name.Trim();
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("prefix is required.", nameof(prefix));
        }

        var normalized = prefix.Trim().ToLowerInvariant().Replace('_', '-');
        if (!PrefixPattern.IsMatch(normalized))
        {
            throw new ArgumentException(
                "prefix must be a short language code (e.g. en, fa, en-us).",
                nameof(prefix));
        }

        return normalized;
    }
}
