using System.Text.Json;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Settings.Domain;

public sealed class SiteSettingsTranslation : Entity
{
    public Guid SiteSettingsId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public string ContactAddressesJson { get; private set; } = "[]";

    public SiteSettings? SiteSettings { get; private set; }

    private SiteSettingsTranslation()
    {
    }

    public static SiteSettingsTranslation Create(
        Guid siteSettingsId,
        string languagePrefix,
        string name,
        string title,
        string? keyword,
        string? description,
        IEnumerable<string> contactAddresses)
    {
        return new SiteSettingsTranslation
        {
            Id = Guid.Empty,
            SiteSettingsId = siteSettingsId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Name = NormalizeRequired(name, nameof(name)),
            Title = NormalizeRequired(title, nameof(title)),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description),
            ContactAddressesJson = JsonSerializer.Serialize(NormalizeList(contactAddresses))
        };
    }

    public void Update(
        string name,
        string title,
        string? keyword,
        string? description,
        IEnumerable<string> contactAddresses)
    {
        Name = NormalizeRequired(name, nameof(name));
        Title = NormalizeRequired(title, nameof(title));
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        ContactAddressesJson = JsonSerializer.Serialize(NormalizeList(contactAddresses));
    }

    public IReadOnlyList<string> GetContactAddresses()
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(ContactAddressesJson) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("languagePrefix is required.", nameof(prefix));
        }

        return prefix.Trim().ToLowerInvariant().Replace('_', '-');
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<string> NormalizeList(IEnumerable<string> values)
    {
        return values
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
