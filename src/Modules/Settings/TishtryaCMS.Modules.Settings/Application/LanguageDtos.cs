namespace TishtryaCMS.Modules.Settings.Application;

public sealed record CreateLanguageRequest(
    string Name,
    string Prefix,
    bool IsDefault,
    string Direction);

public sealed record UpdateLanguageRequest(
    string Name,
    string Prefix,
    bool IsDefault,
    string Direction);

public sealed record LanguageResponse(
    Guid Id,
    string Name,
    string Prefix,
    bool IsDefault,
    string Direction);
