using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class BlogPost : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Keyword { get; private set; }
    public string? Description { get; private set; }
    public string? Content { get; private set; }
    public string? MetaTitle { get; private set; }
    public string? MetaDescription { get; private set; }
    public string Status { get; private set; } = BlogPostStatus.Draft;
    public bool CommentsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public ICollection<BlogPostGroup> PostGroups { get; private set; } = new List<BlogPostGroup>();
    public ICollection<BlogPostTag> PostTags { get; private set; } = new List<BlogPostTag>();
    public ICollection<BlogPostTranslation> Translations { get; private set; } = new List<BlogPostTranslation>();

    private BlogPost()
    {
    }

    public static BlogPost Create(
        string title,
        string slug,
        string? keyword,
        string? description,
        string? content,
        string? metaTitle,
        string? metaDescription,
        string? status,
        bool commentsEnabled = false)
    {
        var now = DateTime.UtcNow;
        return new BlogPost
        {
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Slug = NormalizeSlug(slug),
            Keyword = NormalizeOptional(keyword),
            Description = NormalizeOptional(description),
            Content = NormalizeOptional(content),
            MetaTitle = NormalizeOptional(metaTitle),
            MetaDescription = NormalizeOptional(metaDescription),
            Status = NormalizeStatus(status),
            CommentsEnabled = commentsEnabled,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(
        string title,
        string slug,
        string? keyword,
        string? description,
        string? content,
        string? metaTitle,
        string? metaDescription,
        string? status,
        bool commentsEnabled)
    {
        Title = NormalizeRequired(title, nameof(title));
        Slug = NormalizeSlug(slug);
        Keyword = NormalizeOptional(keyword);
        Description = NormalizeOptional(description);
        Content = NormalizeOptional(content);
        MetaTitle = NormalizeOptional(metaTitle);
        MetaDescription = NormalizeOptional(metaDescription);
        Status = NormalizeStatus(status);
        CommentsEnabled = commentsEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status)
            ? BlogPostStatus.Draft
            : status.Trim().ToLowerInvariant();

        if (!BlogPostStatus.IsValid(value))
        {
            throw new ArgumentException("Status must be 'draft' or 'published'.", nameof(status));
        }

        return value;
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

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
