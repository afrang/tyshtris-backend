using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Settings.Domain;
using TishtryaCMS.Modules.Settings.Infrastructure;

namespace TishtryaCMS.Modules.Settings.Application;

public sealed class SettingsService(SettingsDbContext db, IWebHostEnvironment environment)
{
    public async Task<SiteSettingsResponse> GetAsync(string? lang, CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var settings = await GetOrCreateAsync(cancellationToken);
        return ToResponse(settings, prefix);
    }

    public async Task<PublicSiteSettingsResponse> GetPublicAsync(string? lang, CancellationToken cancellationToken)
    {
        var settings = await GetAsync(lang, cancellationToken);
        return new PublicSiteSettingsResponse(
            settings.Name,
            settings.Title,
            settings.Keyword,
            settings.Description,
            settings.LogoUrl,
            settings.SocialLinks,
            settings.ContactAddresses,
            settings.ContactPhones,
            settings.ContactEmails,
            settings.UpdatedAtUtc,
            settings.LanguagePrefix);
    }

    public async Task<(SiteSettingsResponse? Response, string? Error, int StatusCode)> UpdateAllAsync(
        UpdateAllSettingsRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var settings = await GetOrCreateAsync(cancellationToken);

            var addresses = request.Contact.Addresses ?? [];
            var phones = request.Contact.Phones ?? [];
            var emails = request.Contact.Emails ?? [];

            var translation = await db.SiteSettingsTranslations
                .FirstOrDefaultAsync(
                    x => x.SiteSettingsId == settings.Id && x.LanguagePrefix == prefix,
                    cancellationToken);

            if (translation is null)
            {
                db.SiteSettingsTranslations.Add(SiteSettingsTranslation.Create(
                    settings.Id,
                    prefix,
                    request.General.Name,
                    request.General.Title,
                    request.General.Keyword,
                    request.General.Description,
                    addresses));
            }
            else
            {
                translation.Update(
                    request.General.Name,
                    request.General.Title,
                    request.General.Keyword,
                    request.General.Description,
                    addresses);
            }

            // Denormalized fallback + shared logo on singleton.
            settings.UpdateGeneral(
                request.General.Name,
                request.General.Title,
                request.General.Keyword,
                request.General.Description,
                request.General.LogoUrl ?? settings.LogoUrl);

            settings.UpdateSocialLinks(
                (request.Social.SocialLinks ?? []).Select(x => new SocialLink(x.Platform, x.Url)));

            // Addresses denormalized; phones/emails remain shared.
            settings.UpdateContact(addresses, phones, emails);

            var keepExistingSecret =
                string.IsNullOrWhiteSpace(request.Storage.SecretKey) &&
                !string.IsNullOrWhiteSpace(settings.S3SecretKey);

            settings.UpdateStorage(
                request.Storage.Provider,
                request.Storage.Endpoint,
                request.Storage.Bucket,
                request.Storage.AccessKey,
                keepExistingSecret ? settings.S3SecretKey : request.Storage.SecretKey,
                request.Storage.Region,
                request.Storage.PublicBaseUrl);

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(settings, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(SiteSettingsResponse? Response, string? Error, int StatusCode)> UploadLogoAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            return (null, "Logo file is empty.", StatusCodes.Status400BadRequest);
        }

        var extension = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant();
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "jpg", "jpeg", "png", "webp", "gif", "svg"
        };

        if (!allowed.Contains(extension))
        {
            return (null, "Logo must be an image (jpg, png, webp, gif, svg).", StatusCodes.Status400BadRequest);
        }

        try
        {
            var settings = await GetOrCreateAsync(cancellationToken);

            var uploadsRoot = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads", "settings");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"logo-{Guid.NewGuid():N}.{extension}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = File.Create(physicalPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var publicUrl = $"/uploads/settings/{fileName}";
            settings.SetLogoUrl(publicUrl);
            await db.SaveChangesAsync(cancellationToken);

            return (ToResponse(settings, langPrefix: null), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    private async Task<SiteSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var settings = await db.SiteSettings
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(cancellationToken);

        if (settings is not null)
        {
            return settings;
        }

        settings = SiteSettings.CreateDefault();
        db.SiteSettings.Add(settings);
        await db.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static SiteSettingsResponse ToResponse(SiteSettings settings, string? langPrefix)
    {
        var translation = langPrefix is null
            ? null
            : settings.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new SiteSettingsResponse(
            translation?.Name ?? settings.Name,
            translation?.Title ?? settings.Title,
            translation is null ? settings.Keyword : translation.Keyword,
            translation is null ? settings.Description : translation.Description,
            settings.LogoUrl,
            settings.GetSocialLinks().Select(x => new SocialLinkDto(x.Platform, x.Url)).ToList(),
            translation?.GetContactAddresses() ?? settings.GetContactAddresses(),
            settings.GetContactPhones(),
            settings.GetContactEmails(),
            new StorageSettingsDto(
                settings.StorageProvider,
                settings.S3Endpoint,
                settings.S3Bucket,
                settings.S3AccessKey,
                string.IsNullOrWhiteSpace(settings.S3SecretKey) ? null : "********",
                settings.S3Region,
                settings.S3PublicBaseUrl),
            settings.UpdatedAtUtc,
            langPrefix);
    }
}
