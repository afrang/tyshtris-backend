using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class BlogGroupService(ContentModulesDbContext db)
{
    public async Task<IReadOnlyList<BlogGroupResponse>> GetAllAsync(
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var groups = await db.BlogGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);

        return groups.Select(x => ToResponse(x, prefix)).ToList();
    }

    public async Task<IReadOnlyList<BlogGroupTreeResponse>> GetTreeAsync(
        string? lang,
        CancellationToken cancellationToken)
    {
        var groups = await GetAllAsync(lang, cancellationToken);
        return BuildTree(groups, parentId: null);
    }

    public async Task<(BlogGroupResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        string? lang,
        CancellationToken cancellationToken)
    {
        var group = await db.BlogGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (group is null)
        {
            return (null, "Blog group not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(group, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(BlogGroupResponse? Response, string? Error, int StatusCode)> GetBySlugAsync(
        string slug,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var normalizedSlug = NormalizeSlug(slug);

        BlogGroup? group = null;

        if (prefix is not null)
        {
            var translation = await db.BlogGroupTranslations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.LanguagePrefix == prefix && x.Slug == normalizedSlug,
                    cancellationToken);

            if (translation is not null)
            {
                group = await db.BlogGroups
                    .AsNoTracking()
                    .Include(x => x.Translations)
                    .FirstOrDefaultAsync(x => x.Id == translation.GroupId, cancellationToken);
            }
        }

        group ??= await db.BlogGroups
            .AsNoTracking()
            .Include(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Slug == normalizedSlug, cancellationToken);

        if (group is null)
        {
            return (null, "Blog group not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(group, prefix), null, StatusCodes.Status200OK);
    }

    public async Task<IReadOnlyList<BlogGroupBreadcrumbItem>> GetBreadcrumbAsync(
        Guid groupId,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var trail = new List<BlogGroupBreadcrumbItem>();
        var currentId = (Guid?)groupId;
        var safety = 0;

        while (currentId.HasValue && safety++ < 50)
        {
            var group = await db.BlogGroups
                .AsNoTracking()
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == currentId.Value, cancellationToken);

            if (group is null)
            {
                break;
            }

            var mapped = ToResponse(group, prefix);
            trail.Add(new BlogGroupBreadcrumbItem(mapped.Id, mapped.Title, mapped.Slug));
            currentId = group.ParentId;
        }

        trail.Reverse();
        return trail;
    }

    public async Task<IReadOnlyList<PublicBlogGroupPostItem>> GetPublishedPostsAsync(
        Guid groupId,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var posts = await db.BlogPosts
            .AsNoTracking()
            .Include(x => x.Translations)
            .Where(x => x.Status == BlogPostStatus.Published
                        && x.PostGroups.Any(g => g.GroupId == groupId))
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return posts.Select(post =>
        {
            var t = prefix is null
                ? null
                : post.Translations.FirstOrDefault(x => x.LanguagePrefix == prefix);

            return new PublicBlogGroupPostItem(
                post.Id,
                t?.Title ?? post.Title,
                t?.Slug ?? post.Slug,
                t is null ? post.Description : t.Description,
                post.CreatedAt,
                null);
        }).ToList();
    }

    public async Task<(BlogGroupResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateBlogGroupRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var parentError = await ValidateParentAssignmentAsync(
                groupId: null,
                parentId: request.ParentId,
                cancellationToken);

            if (parentError is not null)
            {
                return (null, parentError, StatusCodes.Status400BadRequest);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludeGroupId: null, cancellationToken))
            {
                return (null, "A blog group with this slug already exists.", StatusCodes.Status409Conflict);
            }

            var group = BlogGroup.Create(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                request.ParentId);

            db.BlogGroups.Add(group);
            await db.SaveChangesAsync(cancellationToken);

            db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
                group.Id,
                prefix,
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description));

            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(group, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(BlogGroupResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateBlogGroupRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var group = await db.BlogGroups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (group is null)
            {
                return (null, "Blog group not found.", StatusCodes.Status404NotFound);
            }

            var parentError = await ValidateParentAssignmentAsync(
                groupId: id,
                parentId: request.ParentId,
                cancellationToken);

            if (parentError is not null)
            {
                return (null, parentError, StatusCodes.Status400BadRequest);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludeGroupId: id, cancellationToken))
            {
                return (null, "A blog group with this slug already exists.", StatusCodes.Status409Conflict);
            }

            group.Update(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                request.ParentId);

            var translation = await db.BlogGroupTranslations
                .FirstOrDefaultAsync(x => x.GroupId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.BlogGroupTranslations.Add(BlogGroupTranslation.Create(
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
            return (ToResponse(group, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var group = await db.BlogGroups.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (group is null)
        {
            return ("Blog group not found.", StatusCodes.Status404NotFound);
        }

        var hasChildren = await db.BlogGroups.AnyAsync(x => x.ParentId == id, cancellationToken);
        if (hasChildren)
        {
            return ("Cannot delete a blog group that has child groups.", StatusCodes.Status409Conflict);
        }

        var hasPosts = await db.BlogPostGroups.AnyAsync(x => x.GroupId == id, cancellationToken);
        if (hasPosts)
        {
            return ("Cannot delete a blog group that is assigned to posts.", StatusCodes.Status409Conflict);
        }

        db.BlogGroups.Remove(group);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private async Task<string?> ValidateParentAssignmentAsync(
        Guid? groupId,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        if (parentId is null)
        {
            return null;
        }

        if (groupId.HasValue && parentId.Value == groupId.Value)
        {
            return "A blog group cannot be its own parent.";
        }

        var parentExists = await db.BlogGroups.AnyAsync(x => x.Id == parentId.Value, cancellationToken);
        if (!parentExists)
        {
            return "Parent blog group does not exist.";
        }

        if (groupId.HasValue)
        {
            var isDescendant = await IsDescendantAsync(ancestorId: groupId.Value, candidateId: parentId.Value, cancellationToken);
            if (isDescendant)
            {
                return "A blog group cannot be moved under one of its own descendants.";
            }
        }

        return null;
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
            var parentId = await db.BlogGroups
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

    private async Task<bool> SlugExistsAsync(
        string slug,
        string languagePrefix,
        Guid? excludeGroupId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeSlug(slug);
        return await db.BlogGroupTranslations.AnyAsync(
            x => x.LanguagePrefix == languagePrefix
                 && x.Slug == normalized
                 && (!excludeGroupId.HasValue || x.GroupId != excludeGroupId.Value),
            cancellationToken);
    }

    private static string NormalizeSlug(string slug) =>
        slug.Trim().ToLowerInvariant().Replace(' ', '-');

    private static BlogGroupResponse ToResponse(BlogGroup group, string? langPrefix)
    {
        var t = langPrefix is null
            ? null
            : group.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

        return new(
            group.Id,
            t?.Title ?? group.Title,
            t?.Slug ?? group.Slug,
            t is null ? group.Keyword : t.Keyword,
            t is null ? group.Description : t.Description,
            group.ParentId,
            langPrefix);
    }

    private static IReadOnlyList<BlogGroupTreeResponse> BuildTree(
        IReadOnlyList<BlogGroupResponse> groups,
        Guid? parentId)
    {
        return groups
            .Where(x => x.ParentId == parentId)
            .Select(x => new BlogGroupTreeResponse(
                x.Id,
                x.Title,
                x.Slug,
                x.Keyword,
                x.Description,
                x.ParentId,
                x.LanguagePrefix,
                BuildTree(groups, x.Id)))
            .ToList();
    }
}
