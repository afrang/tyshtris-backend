using TishtryaCMS.Modules.ContentModules.Application;
using TishtryaCMS.Modules.FileManager.Application;

namespace TishtryaCMS.Api;

public static class PublicMenuEndpoints
{
    public static void MapPublicMenuEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/public/menus/{key}", async (
            string key,
            string? lang,
            MenuItemService menuItemService,
            FileManagerService fileManagerService,
            CancellationToken cancellationToken) =>
            {
                var (tree, error, statusCode) = await menuItemService.GetPublicTreeByKeyAsync(
                    key,
                    lang,
                    cancellationToken);

                if (tree is null)
                {
                    return Results.Json(
                        new { error = error ?? "Menu group not found." },
                        statusCode: statusCode);
                }

                var urls = await fileManagerService.GetPrimaryUrlsByParentIdsAsync(
                    "menuitemimage",
                    CollectIds(tree),
                    cancellationToken);

                return Results.Ok(WithImages(tree, urls));
            })
            .AllowAnonymous()
            .WithName("GetPublicMenuByKey")
            .WithTags("Public");
    }

    private static List<Guid> CollectIds(IReadOnlyList<MenuItemTreeResponse> nodes)
    {
        var ids = new List<Guid>();
        Walk(nodes, ids);
        return ids;

        static void Walk(IReadOnlyList<MenuItemTreeResponse> items, List<Guid> ids)
        {
            foreach (var item in items)
            {
                ids.Add(item.Id);
                if (item.Children.Count > 0)
                {
                    Walk(item.Children, ids);
                }
            }
        }
    }

    private static IReadOnlyList<MenuItemTreeResponse> WithImages(
        IReadOnlyList<MenuItemTreeResponse> nodes,
        IReadOnlyDictionary<Guid, string> urls)
    {
        return nodes
            .Select(node => node with
            {
                ImageUrl = urls.TryGetValue(node.Id, out var url) ? url : null,
                Children = WithImages(node.Children, urls)
            })
            .ToList();
    }
}
