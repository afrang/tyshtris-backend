using System.Text.Json;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Settings.Domain;

public sealed class SiteSettings : Entity
{
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public string Name { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public string? LogoUrl { get; private set; }

    public string SocialLinksJson { get; private set; } = "[]";

    public string ContactAddressesJson { get; private set; } = "[]";
    public string ContactPhonesJson { get; private set; } = "[]";
    public string ContactEmailsJson { get; private set; } = "[]";

    public string StorageProvider { get; private set; } = StorageProviders.Local;
    public string? S3Endpoint { get; private set; }
    public string? S3Bucket { get; private set; }
    public string? S3AccessKey { get; private set; }
    public string? S3SecretKey { get; private set; }
    public string? S3Region { get; private set; }
    public string? S3PublicBaseUrl { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<SiteSettingsTranslation> Translations { get; private set; } = new List<SiteSettingsTranslation>();

    private SiteSettings()
    {
    }

    public static SiteSettings CreateDefault()
    {
        return new SiteSettings
        {
            Id = SingletonId,
            Name = "TishtryaCMS",
            Title = "TishtryaCMS",
            Keyword = null,
            Description = null,
            LogoUrl = null,
            SocialLinksJson = "[]",
            ContactAddressesJson = "[]",
            ContactPhonesJson = "[]",
            ContactEmailsJson = "[]",
            StorageProvider = StorageProviders.Local,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public void UpdateGeneral(string name, string title, string? keyword, string? description, string? logoUrl)
    {
        Name = NormalizeRequired(name, nameof(name));
        Title = NormalizeRequired(title, nameof(title));
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        LogoUrl = NormalizeOptional(logoUrl);
        Touch();
    }

    public void UpdateSocialLinks(IEnumerable<SocialLink> links)
    {
        var normalized = links
            .Select(x => new SocialLink(
                NormalizeRequired(x.Platform, nameof(x.Platform)),
                NormalizeRequired(x.Url, nameof(x.Url))))
            .ToList();

        SocialLinksJson = JsonSerializer.Serialize(normalized);
        Touch();
    }

    public void UpdateContact(
        IEnumerable<string> addresses,
        IEnumerable<string> phones,
        IEnumerable<string> emails)
    {
        ContactAddressesJson = JsonSerializer.Serialize(NormalizeList(addresses));
        ContactPhonesJson = JsonSerializer.Serialize(NormalizeList(phones));
        ContactEmailsJson = JsonSerializer.Serialize(NormalizeList(emails));
        Touch();
    }

    public void UpdateStorage(
        string provider,
        string? endpoint,
        string? bucket,
        string? accessKey,
        string? secretKey,
        string? region,
        string? publicBaseUrl)
    {
        var normalizedProvider = StorageProviders.Normalize(provider);
        if (normalizedProvider is null)
        {
            throw new ArgumentException("Storage provider must be Local or S3.", nameof(provider));
        }

        StorageProvider = normalizedProvider;
        S3Endpoint = NormalizeOptional(endpoint);
        S3Bucket = NormalizeOptional(bucket);
        S3AccessKey = NormalizeOptional(accessKey);
        S3SecretKey = NormalizeOptional(secretKey);
        S3Region = NormalizeOptional(region);
        S3PublicBaseUrl = NormalizeOptional(publicBaseUrl);

        if (StorageProvider == StorageProviders.S3)
        {
            if (string.IsNullOrWhiteSpace(S3Bucket))
            {
                throw new ArgumentException("S3 bucket is required when using S3 storage.", nameof(bucket));
            }

            if (string.IsNullOrWhiteSpace(S3AccessKey) || string.IsNullOrWhiteSpace(S3SecretKey))
            {
                throw new ArgumentException("S3 access key and secret key are required when using S3 storage.");
            }
        }

        Touch();
    }

    public void SetLogoUrl(string? logoUrl)
    {
        LogoUrl = NormalizeOptional(logoUrl);
        Touch();
    }

    public IReadOnlyList<SocialLink> GetSocialLinks()
        => DeserializeList<SocialLink>(SocialLinksJson);

    public IReadOnlyList<string> GetContactAddresses()
        => DeserializeList<string>(ContactAddressesJson);

    public IReadOnlyList<string> GetContactPhones()
        => DeserializeList<string>(ContactPhonesJson);

    public IReadOnlyList<string> GetContactEmails()
        => DeserializeList<string>(ContactEmailsJson);

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static List<string> NormalizeList(IEnumerable<string> values)
    {
        return values
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<T> DeserializeList<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}

public sealed record SocialLink(string Platform, string Url);

public static class StorageProviders
{
    public const string Local = "Local";
    public const string S3 = "S3";

    public static string? Normalize(string? provider)
    {
        if (string.Equals(provider, Local, StringComparison.OrdinalIgnoreCase))
        {
            return Local;
        }

        if (string.Equals(provider, S3, StringComparison.OrdinalIgnoreCase))
        {
            return S3;
        }

        return null;
    }
}
