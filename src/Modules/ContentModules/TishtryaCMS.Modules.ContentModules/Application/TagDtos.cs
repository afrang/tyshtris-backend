namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed record CreateTagRequest(string Title, string Slug, string? Description);

public sealed record UpdateTagRequest(string Title, string Slug, string? Description);

public sealed record TagResponse(Guid Id, string Title, string Slug, string? Description, string? LanguagePrefix);
