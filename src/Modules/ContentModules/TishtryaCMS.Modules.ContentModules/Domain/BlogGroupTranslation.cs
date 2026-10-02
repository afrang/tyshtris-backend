using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogGroupTranslation : Entity
{
    public Guid GroupId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }

    public BlogGroup? Group { get; private set; }

    private BlogGroupTranslation()
    {
    }

    public static BlogGroupTranslation Create(
        Guid groupId,
        string languagePrefix,
        string title,
        string slug,
        string? keyword,
        string? description)
    {
        return new BlogGroupTranslation
        {
            Id = Guid.Empty,
            GroupId = groupId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description)
        };
    }

    public void Update(string title, string slug, string? keyword, string? description)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
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

    private static string NormalizeSlug(string slug) =>
        NormalizeRequired(slug, nameof(slug)).ToLowerInvariant().Replace(' ', '-');

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
