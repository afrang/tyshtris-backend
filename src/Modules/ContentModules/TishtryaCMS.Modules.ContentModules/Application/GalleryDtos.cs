namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed record CreateGalleryRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description);

public sealed record UpdateGalleryRequest(
    string Title,
    string Slug,
    string? Keyword,
    string? Description);

public sealed record GalleryResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    string? Description,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LanguagePrefix);

public sealed record GalleryListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Keyword,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LanguagePrefix);
