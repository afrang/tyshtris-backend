using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class MenuGroupService(ContentModulesDbContext db)
{
    public async Task<IReadOnlyList<MenuGroupResponse>> GetAllAsync(
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var groups = await db.MenuGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return groups.Select(x => ToResponse(x, prefix)).ToList();
    }

    public async Task<(MenuGroupResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        string? lang,
        CancellationToken cancellationToken)
    {
        var group = await db.MenuGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (group is null)
        {
            return (null, "Menu group not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(group, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(MenuGroupResponse? Response, string? Error, int StatusCode)> GetByKeyAsync(
        string key,
        string? lang,
        CancellationToken cancellationToken)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');
        var group = await db.MenuGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Key == normalized && x.IsActive, cancellationToken);

        if (group is null)
        {
            return (null, "Menu group not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(group, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(MenuGroupResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateMenuGroupRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            if (await KeyExistsAsync(request.Key, excludeId: null, cancellationToken))
            {
                return (null, "A menu group with this key already exists.", StatusCodes.Status409Conflict);
            }

            var group = MenuGroup.Create(
                request.Title,
                request.Key,
                request.Description,
                request.SortOrder,
                request.IsActive);

            db.MenuGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuGroupTranslations.Add(MenuGroupTranslation.Create(
                group.Id,
                prefix,
                request.Title,
                request.Description));

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(group, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(MenuGroupResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateMenuGroupRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var group = await db.MenuGroups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (group is null)
            {
                return (null, "Menu group not found.", StatusCodes.Status404NotFound);
            }

            if (await KeyExistsAsync(request.Key, excludeId: id, cancellationToken))
            {
                return (null, "A menu group with this key already exists.", StatusCodes.Status409Conflict);
            }

            group.Update(
                request.Title,
                request.Key,
                request.Description,
                request.SortOrder,
                request.IsActive);

            var translation = await db.MenuGroupTranslations
                .FirstOrDefaultAsync(x => x.GroupId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.MenuGroupTranslations.Add(MenuGroupTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    request.Description));
            }
            else
            {
                translation.Update(request.Title, request.Description);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(group, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await db.MenuGroups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return ("Menu group not found.", StatusCodes.Status404NotFound);
        }

        var hasItems = await db.MenuItems.AnyAsync(x => x.GroupId == id, cancellationToken);
        if (hasItems)
        {
            return ("Cannot delete a menu group that still has menu items.", StatusCodes.Status409Conflict);
        }

        db.MenuGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private async Task<bool> KeyExistsAsync(string key, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');
        return await db.MenuGroups.AnyAsync(
            x => x.Key == normalized && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);
    }

    private static MenuGroupResponse ToResponse(MenuGroup group, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : group.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            group.Id,
            t?.Title ?? group.Title,
            group.Key,
            t is null ? group.Description : t.Description,
            group.SortOrder,
            group.IsActive,
            langPrefix);
    }
}
