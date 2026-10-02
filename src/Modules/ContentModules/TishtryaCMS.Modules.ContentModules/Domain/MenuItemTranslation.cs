using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class MenuItemTranslation : Entity
{
    public Guid ItemId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public string? Data { get; private set; }

    public MenuItem? Item { get; private set; }

    private MenuItemTranslation()
    {
    }

    public static MenuItemTranslation Create(
        Guid itemId,
        string languagePrefix,
        string title,
        string url,
        string? data)
    {
        return new MenuItemTranslation
        {
            Id = Guid.Empty,
            ItemId = itemId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Title = NormalizeRequired(title, nameof(title)),
            Url = (url ?? string.Empty).Trim(),
            Data = NormalizeOptional(data)
        };
    }

    public void Update(string title, string url, string? data)
    {
        Title = NormalizeRequired(title, nameof(title));
        Url = (url ?? string.Empty).Trim();
        Data = NormalizeOptional(data);
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
}
