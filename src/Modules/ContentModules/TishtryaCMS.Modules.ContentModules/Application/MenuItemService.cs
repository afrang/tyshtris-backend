using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;
using TishtryaCMS.Modules.Forms.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class MenuItemService(
    ContentModulesDbContext db,
    FormsDbContext formsDb)
{
    public async Task<IReadOnlyList<MenuItemResponse>> GetByGroupAsync(
        Guid groupId,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var items = await db.MenuItems
            .AsNoTracking()
            .Include(x => x.Translations)
            .Where(x => x.GroupId == groupId)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return items.Select(x => ToResponse(x, prefix)).ToList();
    }

    public async Task<(IReadOnlyList<MenuItemTreeResponse>? Tree, string? Error, int StatusCode)> GetTreeAsync(
        Guid groupId,
        string? lang,
        CancellationToken cancellationToken)
    {
        var groupExists = await db.MenuGroups.AnyAsync(x => x.Id == groupId, cancellationToken);
        if (!groupExists)
        {
            return (null, "Menu group not found.", StatusCodes.Status404NotFound);
        }

        var items = await GetByGroupAsync(groupId, lang, cancellationToken);
        return (BuildTree(items, parentId: null), null, StatusCodes.Status200OK);
    }

    public async Task<(IReadOnlyList<MenuItemTreeResponse>? Tree, string? Error, int StatusCode)> GetPublicTreeByKeyAsync(
        string key,
        string? lang,
        CancellationToken cancellationToken)
    {
        var normalized = key.Trim().ToLowerInvariant().Replace(' ', '-');
        var group = await db.MenuGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == normalized && x.IsActive, cancellationToken);

        if (group is null)
        {
            return (null, "Menu group not found.", StatusCodes.Status404NotFound);
        }

        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var items = await db.MenuItems
            .AsNoTracking()
            .Include(x => x.Translations)
            .Where(x => x.GroupId == group.Id && x.IsActive)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Title)
            .ToListAsync(cancellationToken);

        var responses = items.Select(x => ToResponse(x, prefix)).ToList();
        return (BuildTree(responses, parentId: null), null, StatusCodes.Status200OK);
    }

    public async Task<(MenuItemResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        string? lang,
        CancellationToken cancellationToken)
    {
        var item = await db.MenuItems
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (item is null)
        {
            return (null, "Menu item not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(item, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(MenuItemResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateMenuItemRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var groupExists = await db.MenuGroups.AnyAsync(x => x.Id == request.GroupId, cancellationToken);
            if (!groupExists)
            {
                return (null, "Menu group not found.", StatusCodes.Status404NotFound);
            }

            var function = MenuLinkFunction.Normalize(request.Function);
            var (resolvedUrl, targetId, resolveError) = await ResolveLinkAsync(
                function,
                request.TargetId,
                request.Url,
                prefix,
                cancellationToken);

            if (resolveError is not null)
            {
                return (null, resolveError, StatusCodes.Status400BadRequest);
            }

            var parentError = await ValidateParentAssignmentAsync(
                itemId: null,
                groupId: request.GroupId,
                parentId: request.ParentId,
                cancellationToken);

            if (parentError is not null)
            {
                return (null, parentError, StatusCodes.Status400BadRequest);
            }

            var item = MenuItem.Create(
                request.Title,
                resolvedUrl!,
                request.GroupId,
                request.ParentId,
                request.Data,
                function,
                targetId,
                request.IsMegaMenu,
                request.SortOrder,
                request.IsActive);

            db.MenuItems.Add(item);
            await db.SaveChangesAsync(cancellationToken);

            db.MenuItemTranslations.Add(MenuItemTranslation.Create(
                item.Id,
                prefix,
                request.Title,
                resolvedUrl!,
                request.Data));

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(item, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(MenuItemResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateMenuItemRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var item = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item is null)
            {
                return (null, "Menu item not found.", StatusCodes.Status404NotFound);
            }

            var function = MenuLinkFunction.Normalize(request.Function);
            var (resolvedUrl, targetId, resolveError) = await ResolveLinkAsync(
                function,
                request.TargetId,
                request.Url,
                prefix,
                cancellationToken);

            if (resolveError is not null)
            {
                return (null, resolveError, StatusCodes.Status400BadRequest);
            }

            var parentError = await ValidateParentAssignmentAsync(
                itemId: id,
                groupId: item.GroupId,
                parentId: request.ParentId,
                cancellationToken);

            if (parentError is not null)
            {
                return (null, parentError, StatusCodes.Status400BadRequest);
            }

            item.Update(
                request.Title,
                resolvedUrl!,
                request.ParentId,
                request.Data,
                function,
                targetId,
                request.IsMegaMenu,
                request.SortOrder,
                request.IsActive);

            var translation = await db.MenuItemTranslations
                .FirstOrDefaultAsync(x => x.ItemId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.MenuItemTranslations.Add(MenuItemTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    resolvedUrl!,
                    request.Data));
            }
            else
            {
                translation.Update(request.Title, resolvedUrl!, request.Data);
            }

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(item, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.MenuItems.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return ("Menu item not found.", StatusCodes.Status404NotFound);
        }

        var hasChildren = await db.MenuItems.AnyAsync(x => x.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return ("Cannot delete a menu item that has child items.", StatusCodes.Status409Conflict);
        }

        db.MenuItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> ReorderAsync(
        ReorderMenuItemsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count == 0)
        {
            return ("At least one menu item is required.", StatusCodes.Status400BadRequest);
        }

        var ids = request.Items.Select(x => x.Id).Distinct().ToList();
        if (ids.Count != request.Items.Count)
        {
            return ("Duplicate menu item IDs are not allowed.", StatusCodes.Status400BadRequest);
        }

        var items = await db.MenuItems.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (items.Count != ids.Count)
        {
            return ("One or more menu item IDs are invalid.", StatusCodes.Status400BadRequest);
        }

        var groupId = items[0].GroupId;
        var parentId = items[0].ParentId;
        if (items.Any(x => x.GroupId != groupId || x.ParentId != parentId))
        {
            return ("Only sibling menu items (same parent) can be reordered together.", StatusCodes.Status400BadRequest);
        }

        var siblingCount = await db.MenuItems.CountAsync(
            x => x.GroupId == groupId && x.ParentId == parentId,
            cancellationToken);

        if (siblingCount != items.Count)
        {
            return ("Reorder payload must include every sibling in the list.", StatusCodes.Status400BadRequest);
        }

        var order = 1;
        foreach (var entry in request.Items.OrderBy(x => x.SortOrder))
        {
            items.First(x => x.Id == entry.Id).SetSortOrder(order++);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    private async Task<(string? Url, Guid? TargetId, string? Error)> ResolveLinkAsync(
        string function,
        Guid? targetId,
        string? customUrl,
        string? langPrefix,
        CancellationToken cancellationToken)
    {
        if (function.Equals(MenuLinkFunction.Custom, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(customUrl))
            {
                return (null, null, "url is required for Custom menu items.");
            }

            return (customUrl.Trim(), null, null);
        }

        if (targetId is null || targetId == Guid.Empty)
        {
            return (null, null, "targetId is required for this function.");
        }

        if (function.Equals(MenuLinkFunction.GroupBlog, StringComparison.OrdinalIgnoreCase))
        {
            var group = await db.BlogGroups
                .AsNoTracking()
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == targetId.Value, cancellationToken);

            if (group is null)
            {
                return (null, null, "Selected blog group was not found.");
            }

            var slug = langPrefix is null
                ? group.Slug
                : group.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix)?.Slug ?? group.Slug;

            return ($"/{slug}", targetId, null);
        }

        if (function.Equals(MenuLinkFunction.Post, StringComparison.OrdinalIgnoreCase))
        {
            var post = await db.BlogPosts
                .AsNoTracking()
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == targetId.Value, cancellationToken);

            if (post is null)
            {
                return (null, null, "Selected post was not found.");
            }

            var slug = langPrefix is null
                ? post.Slug
                : post.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix)?.Slug ?? post.Slug;

            return ($"/post/{slug}", targetId, null);
        }

        if (function.Equals(MenuLinkFunction.Gallery, StringComparison.OrdinalIgnoreCase))
        {
            var gallery = await db.Galleries
                .AsNoTracking()
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == targetId.Value, cancellationToken);

            if (gallery is null)
            {
                return (null, null, "Selected gallery was not found.");
            }

            var slug = langPrefix is null
                ? gallery.Slug
                : gallery.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix)?.Slug ?? gallery.Slug;

            return ($"/gallery/{slug}", targetId, null);
        }

        if (function.Equals(MenuLinkFunction.Form, StringComparison.OrdinalIgnoreCase))
        {
            var form = await formsDb.Forms
                .AsNoTracking()
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == targetId.Value, cancellationToken);

            if (form is null)
            {
                return (null, null, "Selected form was not found.");
            }

            var slug = langPrefix is null
                ? form.Slug
                : form.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix)?.Slug ?? form.Slug;

            return ($"/form/{slug}", targetId, null);
        }

        return (null, null, "Unsupported menu function.");
    }

    private async Task<string?> ValidateParentAssignmentAsync(
        Guid? itemId,
        Guid groupId,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (itemId.HasValue && parentId.Value == itemId.Value)
        {
            return "A menu item cannot be its own parent.";
        }

        var parent = await db.MenuItems
            .AsNoTracking()
            .Where(x => x.Id == parentId.Value)
            .Select(x => new { x.Id, x.GroupId, x.ParentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (parent is null)
        {
            return "Parent menu item does not exist.";
        }

        if (parent.GroupId != groupId)
        {
            return "Parent menu item must belong to the same menu group.";
        }

        if (itemId.HasValue)
        {
            var isDescendant = await IsDescendantAsync(ancestorId: itemId.Value, candidateId: parentId.Value, cancellationToken);
            if (isDescendant)
            {
                return "A menu item cannot be moved under one of its own descendants.";
            }
        }

        var parentDepth = await GetDepthAsync(parentId.Value, cancellationToken);
        if (parentDepth >= MenuItem.MaxDepth)
        {
            return $"Menu items support a maximum of {MenuItem.MaxDepth} levels.";
        }

        if (itemId.HasValue)
        {
            var subtreeHeight = await GetSubtreeHeightAsync(itemId.Value, cancellationToken);
            if (parentDepth + subtreeHeight > MenuItem.MaxDepth)
            {
                return $"Moving this item would exceed the maximum of {MenuItem.MaxDepth} levels.";
            }
        }

        return null;
    }

    private async Task<int> GetDepthAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var depth = 1;
        var currentId = itemId;
        var safety = 0;

        while (safety++ < MenuItem.MaxDepth + 2)
        {
            var parentId = await db.MenuItems
                .AsNoTracking()
                .Where(x => x.Id == currentId)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (parentId is null)
            {
                return depth;
            }

            depth++;
            currentId = parentId.Value;
        }

        return depth;
    }

    private async Task<int> GetSubtreeHeightAsync(Guid rootId, CancellationToken cancellationToken)
    {
        var groupId = await db.MenuItems
            .AsNoTracking()
            .Where(x => x.Id == rootId)
            .Select(x => x.GroupId)
            .FirstAsync(cancellationToken);

        var items = await db.MenuItems
            .AsNoTracking()
            .Where(x => x.GroupId == groupId)
            .Select(x => new { x.Id, x.ParentId })
            .ToListAsync(cancellationToken);

        int Height(Guid id)
        {
            var children = items.Where(x => x.ParentId == id).ToList();
            if (children.Count == 0)
            {
                return 1;
            }

            return 1 + children.Max(child => Height(child.Id));
        }

        return Height(rootId);
    }

    private async Task<bool> IsDescendantAsync(
        Guid ancestorId,
        Guid candidateId,
        CancellationToken cancellationToken)
    {
        var currentId = candidateId;
        var safety = 0;

        while (safety++ < 10_000)
        {
            var parentId = await db.MenuItems
                .AsNoTracking()
                .Where(x => x.Id == currentId)
                .Select(x => x.ParentId)
                .FirstOrDefaultAsync(cancellationToken);

            if (parentId is null)
            {
                return false;
            }

            if (parentId.Value == ancestorId)
            {
                return true;
            }

            currentId = parentId.Value;
        }

        return true;
    }

    private static MenuItemResponse ToResponse(MenuItem item, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : item.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            item.Id,
            item.GroupId,
            t?.Title ?? item.Title,
            t?.Url ?? item.Url,
            item.ParentId,
            t is null ? item.Data : t.Data,
            item.Function,
            item.TargetId,
            item.IsMegaMenu,
            item.SortOrder,
            item.IsActive,
            langPrefix);
    }

    private static IReadOnlyList<MenuItemTreeResponse> BuildTree(
        IReadOnlyList<MenuItemResponse> items,
        Guid? parentId)
    {
        return items
            .Where(x => x.ParentId == parentId)
            .Select(x => new MenuItemTreeResponse(
                x.Id,
                x.GroupId,
                x.Title,
                x.Url,
                x.ParentId,
                x.Data,
                x.Function,
                x.TargetId,
                x.IsMegaMenu,
                x.SortOrder,
                x.IsActive,
                x.LanguagePrefix,
                null,
                BuildTree(items, x.Id)))
            .ToList();
    }
}
