using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;
using TishtryaCMS.Modules.ContentModules.Infrastructure;

namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed class BlogPostService(ContentModulesDbContext db)
{
    public async Task<IReadOnlyList<BlogPostListItemResponse>> GetAllAsync(
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var posts = await db.BlogPosts
            .AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.PostGroups).ThenInclude(x => x.Group!).ThenInclude(x => x.Translations)
            .Include(x => x.PostTags).ThenInclude(x => x.Tag!).ThenInclude(x => x.Translations)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return posts.Select(x => ToListItem(x, prefix)).ToList();
    }

    public async Task<(BlogPostResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        string? lang,
        CancellationToken cancellationToken)
    {
        var post = await LoadPostAsync(id, tracking: false, cancellationToken);
        if (post is null)
        {
            return (null, "Blog post not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(post, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(PublicBlogPostDetailResponse? Response, string? Error, int StatusCode)> GetPublishedBySlugAsync(
        string slug,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var normalizedSlug = NormalizeSlug(slug);

        BlogPost? post = null;

        if (prefix is not null)
        {
            var translation = await db.BlogPostTranslations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.LanguagePrefix == prefix && x.Slug == normalizedSlug,
                    cancellationToken);

            if (translation is not null)
            {
                post = await LoadPostAsync(translation.PostId, tracking: false, cancellationToken);
            }
        }

        post ??= await db.BlogPosts
            .AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.PostGroups).ThenInclude(x => x.Group!).ThenInclude(x => x.Translations)
            .Include(x => x.PostTags).ThenInclude(x => x.Tag!).ThenInclude(x => x.Translations)
            .FirstOrDefaultAsync(x => x.Slug == normalizedSlug, cancellationToken);

        if (post is null)
        {
            return (null, "Blog post not found.", StatusCodes.Status404NotFound);
        }

        if (!string.Equals(post.Status, BlogPostStatus.Published, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "Blog post not found.", StatusCodes.Status404NotFound);
        }

        return (ToPublicDetail(post, prefix), null, StatusCodes.Status200OK);
    }

    public async Task<(BlogPostResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateBlogPostRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var groupIds = NormalizeIds(request.Groups);
            var tagIds = NormalizeIds(request.Tags);

            var relationError = await ValidateRelationsAsync(groupIds, tagIds, cancellationToken);
            if (relationError is not null)
            {
                return (null, relationError, StatusCodes.Status400BadRequest);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludePostId: null, cancellationToken))
            {
                return (null, "A blog post with this slug already exists.", StatusCodes.Status409Conflict);
            }

            var post = BlogPost.Create(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                request.Content,
                request.MetaTitle,
                request.MetaDescription,
                request.Status,
                request.CommentsEnabled ?? false);

            db.BlogPosts.Add(post);
            await db.SaveChangesAsync(cancellationToken);

            db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                post.Id,
                prefix,
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                request.Content,
                request.MetaTitle,
                request.MetaDescription));

            await ReplaceGroupsAsync(post.Id, groupIds, cancellationToken);
            await ReplaceTagsAsync(post.Id, tagIds, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var created = await LoadPostAsync(post.Id, tracking: false, cancellationToken);
            return (ToResponse(created!, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(BlogPostResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateBlogPostRequest request,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var post = await db.BlogPosts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (post is null)
            {
                return (null, "Blog post not found.", StatusCodes.Status404NotFound);
            }

            var groupIds = NormalizeIds(request.Groups);
            var tagIds = NormalizeIds(request.Tags);

            var relationError = await ValidateRelationsAsync(groupIds, tagIds, cancellationToken);
            if (relationError is not null)
            {
                return (null, relationError, StatusCodes.Status400BadRequest);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludePostId: id, cancellationToken))
            {
                return (null, "A blog post with this slug already exists.", StatusCodes.Status409Conflict);
            }

            post.Update(
                request.Title,
                request.Slug,
                request.Keyword,
                request.Description,
                request.Content,
                request.MetaTitle,
                request.MetaDescription,
                request.Status,
                request.CommentsEnabled ?? post.CommentsEnabled);

            var translation = await db.BlogPostTranslations
                .FirstOrDefaultAsync(x => x.PostId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.BlogPostTranslations.Add(BlogPostTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    request.Slug,
                    request.Keyword,
                    request.Description,
                    request.Content,
                    request.MetaTitle,
                    request.MetaDescription));
            }
            else
            {
                translation.Update(
                    request.Title,
                    request.Slug,
                    request.Keyword,
                    request.Description,
                    request.Content,
                    request.MetaTitle,
                    request.MetaDescription);
            }

            await ReplaceGroupsAsync(id, groupIds, cancellationToken);
            await ReplaceTagsAsync(id, tagIds, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);

            var updated = await LoadPostAsync(id, tracking: false, cancellationToken);
            return (ToResponse(updated!, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var post = await db.BlogPosts
            .Include(x => x.PostGroups)
            .Include(x => x.PostTags)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (post is null)
        {
            return ("Blog post not found.", StatusCodes.Status404NotFound);
        }

        db.BlogPostGroups.RemoveRange(post.PostGroups);
        db.BlogPostTags.RemoveRange(post.PostTags);
        db.BlogPosts.Remove(post);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private async Task ReplaceGroupsAsync(Guid postId, IReadOnlyList<Guid> groupIds, CancellationToken cancellationToken)
    {
        var existing = await db.BlogPostGroups.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        db.BlogPostGroups.RemoveRange(existing);

        foreach (var groupId in groupIds)
        {
            db.BlogPostGroups.Add(BlogPostGroup.Create(postId, groupId));
        }
    }

    private async Task ReplaceTagsAsync(Guid postId, IReadOnlyList<Guid> tagIds, CancellationToken cancellationToken)
    {
        var existing = await db.BlogPostTags.Where(x => x.PostId == postId).ToListAsync(cancellationToken);
        db.BlogPostTags.RemoveRange(existing);

        foreach (var tagId in tagIds)
        {
            db.BlogPostTags.Add(BlogPostTag.Create(postId, tagId));
        }
    }

    private async Task<string?> ValidateRelationsAsync(
        IReadOnlyList<Guid> groupIds,
        IReadOnlyList<Guid> tagIds,
        CancellationToken cancellationToken)
    {
        if (groupIds.Count > 0)
        {
            var existingCount = await db.BlogGroups.CountAsync(x => groupIds.Contains(x.Id), cancellationToken);
            if (existingCount != groupIds.Count)
            {
                return "One or more blog group IDs are invalid.";
            }
        }

        if (tagIds.Count > 0)
        {
            var existingCount = await db.Tags.CountAsync(x => tagIds.Contains(x.Id), cancellationToken);
            if (existingCount != tagIds.Count)
            {
                return "One or more tag IDs are invalid.";
            }
        }

        return null;
    }

    private static IReadOnlyList<Guid> NormalizeIds(IReadOnlyList<Guid>? ids) =>
        (ids ?? [])
            .Where(x => x != Guid.Empty)
            .Distinct()
            .ToList();

    private async Task<bool> SlugExistsAsync(
        string slug,
        string languagePrefix,
        Guid? excludePostId,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant().Replace(' ', '-');
        return await db.BlogPostTranslations.AnyAsync(
            x => x.LanguagePrefix == languagePrefix
                 && x.Slug == normalized
                 && (!excludePostId.HasValue || x.PostId != excludePostId.Value),
            cancellationToken);
    }

    private async Task<BlogPost?> LoadPostAsync(Guid id, bool tracking, CancellationToken cancellationToken)
    {
        IQueryable<BlogPost> query = db.BlogPosts
            .Include(x => x.Translations)
            .Include(x => x.PostGroups).ThenInclude(x => x.Group!).ThenInclude(x => x.Translations)
            .Include(x => x.PostTags).ThenInclude(x => x.Tag!).ThenInclude(x => x.Translations);

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    private static BlogPostListItemResponse ToListItem(BlogPost post, string? langPrefix)
    {
        var t = FindTranslation(post, langPrefix);
        return new(
            post.Id,
            t?.Title ?? post.Title,
            t?.Slug ?? post.Slug,
            post.Status,
            post.CreatedAt,
            MapGroups(post, langPrefix),
            MapTags(post, langPrefix),
            langPrefix);
    }

    private static BlogPostResponse ToResponse(BlogPost post, string? langPrefix)
    {
        var t = FindTranslation(post, langPrefix);
        return new(
            post.Id,
            t?.Title ?? post.Title,
            t?.Slug ?? post.Slug,
            t is null ? post.Keyword : t.Keyword,
            t is null ? post.Description : t.Description,
            t is null ? post.Content : t.Content,
            t is null ? post.MetaTitle : t.MetaTitle,
            t is null ? post.MetaDescription : t.MetaDescription,
            post.Status,
            post.CommentsEnabled,
            post.CreatedAt,
            post.UpdatedAt,
            MapGroups(post, langPrefix),
            MapTags(post, langPrefix),
            langPrefix);
    }

    private static PublicBlogPostDetailResponse ToPublicDetail(BlogPost post, string? langPrefix)
    {
        var t = FindTranslation(post, langPrefix);
        return new(
            post.Id,
            t?.Title ?? post.Title,
            t?.Slug ?? post.Slug,
            t is null ? post.Keyword : t.Keyword,
            t is null ? post.Description : t.Description,
            t is null ? post.MetaTitle : t.MetaTitle,
            t is null ? post.MetaDescription : t.MetaDescription,
            post.CommentsEnabled,
            post.CreatedAt,
            post.UpdatedAt,
            MapGroupsWithSlug(post, langPrefix),
            MapTags(post, langPrefix),
            langPrefix,
            t is null ? post.Content : t.Content);
    }

    private static string NormalizeSlug(string slug) =>
        slug.Trim().ToLowerInvariant().Replace(' ', '-');

    private static BlogPostTranslation? FindTranslation(BlogPost post, string? langPrefix) =>
        langPrefix is null
            ? null
            : post.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);

    private static IReadOnlyList<RelatedItemResponse> MapGroups(BlogPost post, string? langPrefix) =>
        post.PostGroups
            .Where(x => x.Group is not null)
            .Select(x =>
            {
                var title = langPrefix is null
                    ? x.Group!.Title
                    : x.Group!.Translations.FirstOrDefault(t => t.LanguagePrefix == langPrefix)?.Title
                      ?? x.Group.Title;
                return new RelatedItemResponse(x.GroupId, title);
            })
            .OrderBy(x => x.Title)
            .ToList();

    private static IReadOnlyList<RelatedGroupItemResponse> MapGroupsWithSlug(BlogPost post, string? langPrefix) =>
        post.PostGroups
            .Where(x => x.Group is not null)
            .Select(x =>
            {
                var translation = langPrefix is null
                    ? null
                    : x.Group!.Translations.FirstOrDefault(t => t.LanguagePrefix == langPrefix);
                return new RelatedGroupItemResponse(
                    x.GroupId,
                    translation?.Title ?? x.Group!.Title,
                    translation?.Slug ?? x.Group!.Slug);
            })
            .OrderBy(x => x.Title)
            .ToList();

    private static IReadOnlyList<RelatedItemResponse> MapTags(BlogPost post, string? langPrefix) =>
        post.PostTags
            .Where(x => x.Tag is not null)
            .Select(x =>
            {
                var title = langPrefix is null
                    ? x.Tag!.Title
                    : x.Tag!.Translations.FirstOrDefault(t => t.LanguagePrefix == langPrefix)?.Title
                      ?? x.Tag.Title;
                return new RelatedItemResponse(x.TagId, title);
            })
            .OrderBy(x => x.Title)
            .ToList();
}
