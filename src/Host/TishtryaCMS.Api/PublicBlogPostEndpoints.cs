using TishtryaCMS.Modules.ContentModules.Application;
using TishtryaCMS.Modules.EditorTrya.Application;
using TishtryaCMS.Modules.FileManager.Application;

namespace TishtryaCMS.Api;

public static class PublicBlogPostEndpoints
{
    public static void MapPublicBlogPostEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/public/posts/{slug}", async (
            string slug,
            string? lang,
            BlogPostService blogPostService,
            BlogGroupService blogGroupService,
            EditorTryaService editorTryaService,
            FileManagerService fileManagerService,
            CancellationToken cancellationToken) =>
            {
                var (post, error, statusCode) = await blogPostService.GetPublishedBySlugAsync(
                    slug,
                    lang,
                    cancellationToken);

                if (post is null)
                {
                    return Results.Json(new { error = error ?? "Blog post not found." }, statusCode: statusCode);
                }

                var breadcrumbs = new List<BlogGroupBreadcrumbItem>();
                var primaryGroup = post.Groups.FirstOrDefault();
                if (primaryGroup is not null)
                {
                    var trail = await blogGroupService.GetBreadcrumbAsync(
                        primaryGroup.Id,
                        lang,
                        cancellationToken);
                    breadcrumbs.AddRange(trail);
                }

                breadcrumbs.Add(new BlogGroupBreadcrumbItem(post.Id, post.Title, post.Slug));

                var thumbnails = await fileManagerService.GetPrimaryUrlsByParentIdsAsync(
                    "blogthumbnail",
                    [post.Id],
                    cancellationToken);

                EditorTreeResponse? content = null;
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    var (tree, contentError, contentStatus) = await editorTryaService.GetPublishedEditorAsync(
                        "blogpost",
                        post.Id,
                        lang,
                        cancellationToken);

                    if (contentError is not null)
                    {
                        return Results.Json(new { error = contentError }, statusCode: contentStatus);
                    }

                    content = tree;
                }

                var mediaMap = await PublicEditorMediaMap.BuildAsync(
                    content,
                    fileManagerService,
                    cancellationToken);

                var response = new PublicBlogPostPageResponse(
                    post.Id,
                    post.Title,
                    post.Slug,
                    post.Keyword,
                    post.Description,
                    post.MetaTitle,
                    post.MetaDescription,
                    post.CommentsEnabled,
                    post.CreatedAt,
                    post.UpdatedAt,
                    post.LanguagePrefix,
                    thumbnails.TryGetValue(post.Id, out var thumb) ? thumb : null,
                    breadcrumbs,
                    post.Groups,
                    post.Tags,
                    content,
                    post.Content,
                    mediaMap);

                return Results.Ok(response);
            })
            .AllowAnonymous()
            .WithName("GetPublicBlogPostBySlug")
            .WithTags("Public");
    }
}
