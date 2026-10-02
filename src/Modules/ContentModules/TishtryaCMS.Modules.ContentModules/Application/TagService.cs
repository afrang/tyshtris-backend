using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class TagService(ContentModulesDbContext db)
{
    public async Task<IReadOnlyList<TagResponse>> GetAllAsync(string? lang, CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var tags = await db.Tags
            .AsNoTracking()
            .Include(x => x.Translations)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return tags.Select(x => ToResponse(x, prefix)).ToList();
    }

    public async Task<IReadOnlyList<TagResponse>> SearchAsync(
        string? query,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var q = (query ?? string.Empty).Trim().ToLowerInvariant();

        var tags = await db.Tags
            .AsNoTracking()
            .Include(x => x.Translations)
            .ToListAsync(cancellationToken);

        IEnumerable<Tag> filtered = tags;
        if (!string.IsNullOrWhiteSpace(q))
        {
            filtered = tags.Where(x =>
            {
                var t = prefix is null
                    ? null
                    : x.Translations.FirstOrDefault(tr => tr.LanguagePrefix == prefix);
                var title = (t?.Title ?? x.Title).ToLowerInvariant();
                var slug = (t?.Slug ?? x.Slug).ToLowerInvariant();
                return title.Contains(q) || slug.Contains(q);
            });
        }

        return filtered
            .Select(x => ToResponse(x, prefix))
            .OrderBy(x => x.Title)
            .Take(50)
            .ToList();
    }

    public async Task<(TagResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        string? lang,
        CancellationToken cancellationToken)
    {
        var tag = await db.Tags
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (tag is null)
        {
            return (null, "Tag not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(tag, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(TagResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateTagRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            if (await SlugExistsAsync(request.Slug, prefix, excludeTagId: null, cancellationToken))
            {
                return (null, "A tag with this slug already exists.", StatusCodes.Status409Conflict);
            }

            var tag = Tag.Create(request.Title, request.Slug, request.Description);
            db.Tags.Add(tag);
            await db.SaveChangesAsync(cancellationToken);

            db.TagTranslations.Add(TagTranslation.Create(
                tag.Id,
                prefix,
                request.Title,
                request.Slug,
                request.Description));

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(tag, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(TagResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateTagRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var tag = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (tag is null)
            {
                return (null, "Tag not found.", StatusCodes.Status404NotFound);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludeTagId: id, cancellationToken))
            {
                return (null, "A tag with this slug already exists.", StatusCodes.Status409Conflict);
            }

            tag.Update(request.Title, request.Slug, request.Description);

            var translation = await db.TagTranslations
                .FirstOrDefaultAsync(x => x.TagId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.TagTranslations.Add(TagTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    request.Slug,
                    request.Description));
            }
            else
            {
                translation.Update(request.Title, request.Slug, request.Description);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(tag, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tag is null)
        {
            return ("Tag not found.", StatusCodes.Status404NotFound);
        }

        var inUse = await db.BlogPostTags.AnyAsync(x => x.TagId == id, cancellationToken);
        if (inUse)
        {
            return ("Cannot delete a tag that is assigned to posts.", StatusCodes.Status409Conflict);
        }

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        string languagePrefix,
        Guid? excludeTagId,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant().Replace(' ', '-');
        return await db.TagTranslations.AnyAsync(
            x => x.LanguagePrefix == languagePrefix
                 && x.Slug == normalized
                 && (!excludeTagId.HasValue || x.TagId != excludeTagId.Value),
            cancellationToken);
    }

    private static TagResponse ToResponse(Tag tag, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : tag.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            tag.Id,
            t?.Title ?? tag.Title,
            t?.Slug ?? tag.Slug,
            t is null ? tag.Description : t.Description,
            langPrefix);
    }
}
