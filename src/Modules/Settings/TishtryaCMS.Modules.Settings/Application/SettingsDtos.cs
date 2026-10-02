using TishtryaCMS.Modules.Settings.Domain;

namespace TishtryaCMS.Modules.Settings.Application;

public sealed record SocialLinkDto(string Platform, string Url);

public sealed record StorageSettingsDto(
    string Provider,
    string? Endpoint,
    string? Bucket,
    string? AccessKey,
    string? SecretKey,
    string? Region,
    string? PublicBaseUrl);

public sealed record SiteSettingsResponse(
    string Name,
    string Title,
    string? Keyword,
    string? Description,
    string? LogoUrl,
    IReadOnlyList<SocialLinkDto> SocialLinks,
    IReadOnlyList<string> ContactAddresses,
    IReadOnlyList<string> ContactPhones,
    IReadOnlyList<string> ContactEmails,
    StorageSettingsDto Storage,
    DateTime UpdatedAtUtc,
    string? LanguagePrefix);

/// <summary>Public-safe settings payload (no storage credentials).</summary>
public sealed record PublicSiteSettingsResponse(
    string Name,
    string Title,
    string? Keyword,
    string? Description,
    string? LogoUrl,
    IReadOnlyList<SocialLinkDto> SocialLinks,
    IReadOnlyList<string> ContactAddresses,
    IReadOnlyList<string> ContactPhones,
    IReadOnlyList<string> ContactEmails,
    DateTime UpdatedAtUtc,
    string? LanguagePrefix);

public sealed record UpdateGeneralSettingsRequest(
    string Name,
    string Title,
    string? Keyword,
    string? Description,
    string? LogoUrl);

public sealed record UpdateSocialSettingsRequest(IReadOnlyList<SocialLinkDto> SocialLinks);

public sealed record UpdateContactSettingsRequest(
    IReadOnlyList<string> Addresses,
    IReadOnlyList<string> Phones,
    IReadOnlyList<string> Emails);

public sealed record UpdateStorageSettingsRequest(
    string Provider,
    string? Endpoint,
    string? Bucket,
    string? AccessKey,
    string? SecretKey,
    string? Region,
    string? PublicBaseUrl);

public sealed record UpdateAllSettingsRequest(
    UpdateGeneralSettingsRequest General,
    UpdateSocialSettingsRequest Social,
    UpdateContactSettingsRequest Contact,
    UpdateStorageSettingsRequest Storage);
