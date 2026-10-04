namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed record CreateBlogPostRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    string? Content,
    string? MetaTitle,
    string? MetaDescription,
    string? Status,
    bool? CommentsEnabled,
    IReadOnlyList<Guid>? Groups,
    IReadOnlyList<Guid>? Tags);

public sealed record UpdateBlogPostRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    string? Content,
    string? MetaTitle,
    string? MetaDescription,
    string? Status,
    bool? CommentsEnabled,
    IReadOnlyList<Guid>? Groups,
    IReadOnlyList<Guid>? Tags);

public sealed record RelatedItemResponse(Guid Id, string Title);

public sealed record RelatedGroupItemResponse(Guid Id, string Title, string Slug);

public sealed record BlogPostResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    string? Content,
    string? MetaTitle,
    string? MetaDescription,
    string Status,
    bool CommentsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<RelatedItemResponse> Groups,
    IReadOnlyList<RelatedItemResponse> Tags,
    string? LanguagePrefix);

/// <summary>Published post payload used by the public site (includes group slugs).</summary>
public sealed record PublicBlogPostDetailResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    bool CommentsEnabled,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<RelatedGroupItemResponse> Groups,
    IReadOnlyList<RelatedItemResponse> Tags,
    string? LanguagePrefix,
    string? Content);

public sealed record PublicBlogPostPageResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    string? MetaTitle,
    string? MetaDescription,
    bool CommentsEnabled,
    bool ShowTimestamp,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LanguagePrefix,
    string? ThumbnailUrl,
    IReadOnlyList<BlogGroupBreadcrumbItem> Breadcrumb,
    IReadOnlyList<RelatedGroupItemResponse> Groups,
    IReadOnlyList<RelatedItemResponse> Tags,
    object? Content,
    string? FallbackHtml,
    IReadOnlyDictionary<string, string> MediaMap);

public sealed record BlogPostListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<RelatedItemResponse> Groups,
    IReadOnlyList<RelatedItemResponse> Tags,
    string? LanguagePrefix);
