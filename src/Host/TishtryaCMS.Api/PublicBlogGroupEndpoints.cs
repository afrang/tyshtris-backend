using TishtryaCMS.Modules.ContentModules.Application;
using TishtryaCMS.Modules.EditorTrya.Application;
using TishtryaCMS.Modules.FileManager.Application;

namespace TishtryaCMS.Api;

public static class PublicBlogGroupEndpoints
{
    public static void MapPublicBlogGroupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/public/blog-groups/{slug}", async (
            string slug,
            string? lang,
            BlogGroupService blogGroupService,
            EditorTryaService editorTryaService,
            FileManagerService fileManagerService,
            CancellationToken cancellationToken) =>
            {
                var (group, error, statusCode) = await blogGroupService.GetBySlugAsync(
                    slug,
                    lang,
                    cancellationToken);

                if (group is null)
                {
                    return Results.Json(new { error = error ?? "Blog group not found." }, statusCode: statusCode);
                }

                var breadcrumb = await blogGroupService.GetBreadcrumbAsync(
                    group.Id,
                    lang,
                    cancellationToken);

                var posts = await blogGroupService.GetPublishedPostsAsync(
                    group.Id,
                    lang,
                    cancellationToken);

                var postThumbnails = await fileManagerService.GetPrimaryUrlsByParentIdsAsync(
                    "blogthumbnail",
                    posts.Select(x => x.Id),
                    cancellationToken);

                var groupThumbnails = await fileManagerService.GetPrimaryUrlsByParentIdsAsync(
                    "bloggroupthumbnail",
                    [group.Id],
                    cancellationToken);

                var postsWithThumbs = posts
                    .Select(post => post with
                    {
                        ThumbnailUrl = postThumbnails.TryGetValue(post.Id, out var url) ? url : null
                    })
                    .ToList();

                EditorTreeResponse? content = null;
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    var (tree, contentError, contentStatus) = await editorTryaService.GetPublishedEditorAsync(
                        "bloggroup",
                        group.Id,
                        lang,
                        cancellationToken);

                    if (contentError is not null)
                    {
                        return Results.Json(new { error = contentError }, statusCode: contentStatus);
                    }

                    content = tree;
                }

                var mediaMap = await PublicEditorMediaMap.BuildAsync(content, fileManagerService, cancellationToken);

                var response = new PublicBlogGroupPageResponse(
                    group.Id,
                    group.Title,
                    group.Slug,
                    group.Keyword,
                    group.Description,
                    group.ParentId,
                    group.ShowTimestamp,
                    group.LanguagePrefix,
                    groupThumbnails.TryGetValue(group.Id, out var groupThumb) ? groupThumb : null,
                    breadcrumb,
                    content,
                    mediaMap,
                    postsWithThumbs);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("GetPublicBlogGroupBySlug")
            .WithTags("Public");
    }
}
