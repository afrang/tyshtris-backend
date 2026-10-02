using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class GalleryService(ContentModulesDbContext db)
{
    public async Task<IReadOnlyList<GalleryListItemResponse>> GetAllAsync(
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var query = db.Galleries.AsNoTracking().Include(x => x.Translations).AsQueryable();

        var userId = TryGetUserId(user);
        if (userId.HasValue && !IsAdmin(user))
        {
            query = query.Where(x => x.CreatedBy == userId.Value);
        }

        var galleries = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return galleries.Select(x => ToListItem(x, prefix)).ToList();
    }

    public async Task<(GalleryResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        var gallery = await db.Galleries
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (gallery is null)
        {
            return (null, "Gallery not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(gallery, user);
        if (accessError is not null)
        {
            return (null, accessError, StatusCodes.Status403Forbidden);
        }

        return (ToResponse(gallery, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(GalleryResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateGalleryRequest request,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            if (await SlugExistsAsync(request.Slug, prefix, excludeGalleryId: null, cancellationToken))
            {
                return (null, "A gallery with this slug already exists.", StatusCodes.Status409Conflict);
            }

            var gallery = Gallery.Create(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                TryGetUserId(user));

            db.Galleries.Add(gallery);
            await db.SaveChangesAsync(cancellationToken);

            db.GalleryTranslations.Add(GalleryTranslation.Create(
                gallery.Id,
                prefix,
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description));

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(gallery, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(GalleryResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateGalleryRequest request,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var gallery = await db.Galleries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (gallery is null)
            {
                return (null, "Gallery not found.", StatusCodes.Status404NotFound);
            }

            var accessError = EnsureCanAccess(gallery, user);
            if (accessError is not null)
            {
                return (null, accessError, StatusCodes.Status403Forbidden);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludeGalleryId: id, cancellationToken))
            {
                return (null, "A gallery with this slug already exists.", StatusCodes.Status409Conflict);
            }

            gallery.Update(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description);

            var translation = await db.GalleryTranslations
                .FirstOrDefaultAsync(x => x.GalleryId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.GalleryTranslations.Add(GalleryTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    request.Slug,
                    request.Keyword,
                    request.Description));
            }
            else
            {
                translation.Update(request.Title, request.Slug, request.Keyword, request.Description);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(gallery, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(
        Guid id,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var gallery = await db.Galleries.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (gallery is null)
        {
            return ("Gallery not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(gallery, user);
        if (accessError is not null)
        {
            return (accessError, StatusCodes.Status403Forbidden);
        }

        db.Galleries.Remove(gallery);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private static string? EnsureCanAccess(Gallery gallery, ClaimsPrincipal? user)
    {
        if (IsAdmin(user))
        {
            return null;
        }

        var userId = TryGetUserId(user);
        if (!userId.HasValue)
        {
            return "Authentication required.";
        }

        if (gallery.CreatedBy.HasValue && gallery.CreatedBy.Value != userId.Value)
        {
            return "You can only manage galleries you created.";
        }

        return null;
    }

    private static bool IsAdmin(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return false;
        }

        return user.IsInRole("SuperAdmin") || user.IsInRole("Admin");
    }

    private static Guid? TryGetUserId(ClaimsPrincipal? user)
    {
        var raw = user?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user?.FindFirstValue("sub");

        return Guid.TryParse(raw, out var id) ? id : null;
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        string languagePrefix,
        Guid? excludeGalleryId,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant().Replace(' ', '-');
        return await db.GalleryTranslations.AnyAsync(
            x => x.LanguagePrefix == languagePrefix
                 && x.Slug == normalized
                 && (!excludeGalleryId.HasValue || x.GalleryId != excludeGalleryId.Value),
            cancellationToken);
    }

    private static GalleryListItemResponse ToListItem(Gallery gallery, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : gallery.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            gallery.Id,
            t?.Title ?? gallery.Title,
            t?.Slug ?? gallery.Slug,
            t is null ? gallery.Keyword : t.Keyword,
            gallery.CreatedBy,
            gallery.CreatedAt,
            gallery.UpdatedAt,
            langPrefix);
    }

    private static GalleryResponse ToResponse(Gallery gallery, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : gallery.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            gallery.Id,
            t?.Title ?? gallery.Title,
            t?.Slug ?? gallery.Slug,
            t is null ? gallery.Keyword : t.Keyword,
            t is null ? gallery.Description : t.Description,
            gallery.CreatedBy,
            gallery.CreatedAt,
            gallery.UpdatedAt,
            langPrefix);
    }
}
