namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed record CreateBlogGroupRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? ParentId);

public sealed record UpdateBlogGroupRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? ParentId);

public sealed record BlogGroupResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? ParentId,
    string? LanguagePrefix);

public sealed record BlogGroupTreeResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? ParentId,
    string? LanguagePrefix,
    IReadOnlyList<BlogGroupTreeResponse> Children);

public sealed record BlogGroupBreadcrumbItem(
    Guid Id,
    string Title,
    string Slug);

public sealed record PublicBlogGroupPostItem(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    string? ThumbnailUrl);

public sealed record PublicBlogGroupPageResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? ParentId,
    string? LanguagePrefix,
    string? ThumbnailUrl,
    IReadOnlyList<BlogGroupBreadcrumbItem> Breadcrumb,
    object? Content,
    IReadOnlyDictionary<string, string> MediaMap,
    IReadOnlyList<PublicBlogGroupPostItem> Posts);
