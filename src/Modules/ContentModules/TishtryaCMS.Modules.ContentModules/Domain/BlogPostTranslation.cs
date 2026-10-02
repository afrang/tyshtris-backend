using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogPostTranslation : Entity
{
    public Guid PostId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public string? Content { get; private set; }
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }

    public BlogPost? Post { get; private set; }

    private BlogPostTranslation()
    {
    }

    public static BlogPostTranslation Create(
        Guid postId,
        string languagePrefix,
        string title,
        string slug,
        string? keyword,
        string? description,
        string? content,
        string? metaTitle,
        string? metaDescription)
    {
        return new BlogPostTranslation
        {
            Id = Guid.Empty,
            PostId = postId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description),
            Content = NormalizeOptional(content),
            MetaTitle = NormalizeOptional(metaTitle),
            MetaDescription = NormalizeOptional(metaDescription)
        };
    }

    public void Update(
        string title,
        string slug,
        string? keyword,
        string? description,
        string? content,
        string? metaTitle,
        string? metaDescription)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        Content = NormalizeOptional(content);
        MetaTitle = NormalizeOptional(metaTitle);
        MetaDescription = NormalizeOptional(metaDescription);
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

    private static string NormalizeSlug(string slug)
    {
        var normalized = NormalizeRequired(slug, nameof(slug)).ToLowerInvariant();
        return normalized.Replace(' ', '-');
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
