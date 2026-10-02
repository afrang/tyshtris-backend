using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.ContentModules.Application;
using TishtryaCMS.Modules.ContentModules.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules;

public sealed class ContentModulesModule : IModule
{
    public string Name => "ContentModules";

    public void Register(IServiceCollection services)
    {
        // Registered via Register(services, configuration) from the host.
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<ContentModulesDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<BlogGroupService>();
        services.AddScoped<BlogPostService>();
        services.AddScoped<TagService>();
        services.AddScoped<GalleryService>();
        services.AddScoped<MenuGroupService>();
        services.AddScoped<MenuItemService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapBlogGroupEndpoints(endpoints);
        MapBlogPostEndpoints(endpoints);
        MapTagEndpoints(endpoints);
        MapGalleryEndpoints(endpoints);
        MapMenuGroupEndpoints(endpoints);
        MapMenuItemEndpoints(endpoints);
    }

    private static void MapBlogGroupEndpoints(IEndpointRouteBuilder endpoints)
    {
        var blogGroups = endpoints.MapGroup("/api/admin/blog-groups")
            .WithTags("BlogGroups")
            .RequireAuthorization();

        blogGroups.MapGet("/", async (string? lang, BlogGroupService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListBlogGroups");

        blogGroups.MapGet("/tree", async (string? lang, BlogGroupService service, CancellationToken cancellationToken) =>
            {
                var tree = await service.GetTreeAsync(lang, cancellationToken);
                return Results.Ok(tree);
            })
            .WithName("GetBlogGroupTree");

        blogGroups.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            BlogGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetBlogGroupById");

        blogGroups.MapPost("/", async (
            CreateBlogGroupRequest request,
            string? lang,
            BlogGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/blog-groups/{response!.Id}", response);
            })
            .WithName("CreateBlogGroup");

        blogGroups.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBlogGroupRequest request,
            string? lang,
            BlogGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateBlogGroup");

        blogGroups.MapDelete("/{id:guid}", async (Guid id, BlogGroupService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteBlogGroup");
    }

    private static void MapBlogPostEndpoints(IEndpointRouteBuilder endpoints)
    {
        var posts = endpoints.MapGroup("/api/admin/blog-posts")
            .WithTags("BlogPosts")
            .RequireAuthorization();

        posts.MapGet("/", async (string? lang, BlogPostService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListBlogPosts");

        posts.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            BlogPostService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetBlogPostById");

        posts.MapPost("/", async (
            CreateBlogPostRequest request,
            string? lang,
            BlogPostService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/blog-posts/{response!.Id}", response);
            })
            .WithName("CreateBlogPost");

        posts.MapPut("/{id:guid}", async (
            Guid id,
            UpdateBlogPostRequest request,
            string? lang,
            BlogPostService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateBlogPost");

        posts.MapDelete("/{id:guid}", async (Guid id, BlogPostService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteBlogPost");
    }

    private static void MapTagEndpoints(IEndpointRouteBuilder endpoints)
    {
        var tags = endpoints.MapGroup("/api/admin/tags")
            .WithTags("Tags")
            .RequireAuthorization();

        tags.MapGet("/", async (string? lang, TagService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListTags");

        tags.MapGet("/search", async (
            string? q,
            string? lang,
            TagService service,
            CancellationToken cancellationToken) =>
            {
                var items = await service.SearchAsync(q, lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("SearchTags");

        tags.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            TagService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetTagById");

        tags.MapPost("/", async (
            CreateTagRequest request,
            string? lang,
            TagService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/tags/{response!.Id}", response);
            })
            .WithName("CreateTag");

        tags.MapPut("/{id:guid}", async (
            Guid id,
            UpdateTagRequest request,
            string? lang,
            TagService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateTag");

        tags.MapDelete("/{id:guid}", async (Guid id, TagService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteTag");
    }

    private static void MapGalleryEndpoints(IEndpointRouteBuilder endpoints)
    {
        var galleries = endpoints.MapGroup("/api/admin/galleries")
            .WithTags("Galleries")
            .RequireAuthorization();

        galleries.MapGet("/", async (
            string? lang,
            HttpContext http,
            GalleryService service,
            CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(http.User, lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListGalleries");

        galleries.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            HttpContext http,
            GalleryService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, http.User, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetGalleryById");

        galleries.MapPost("/", async (
            CreateGalleryRequest request,
            string? lang,
            HttpContext http,
            GalleryService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, http.User, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/galleries/{response!.Id}", response);
            })
            .WithName("CreateGallery");

        galleries.MapPut("/{id:guid}", async (
            Guid id,
            UpdateGalleryRequest request,
            string? lang,
            HttpContext http,
            GalleryService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, http.User, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateGallery");

        galleries.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext http,
            GalleryService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, http.User, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteGallery");
    }

    private static void MapMenuGroupEndpoints(IEndpointRouteBuilder endpoints)
    {
        var groups = endpoints.MapGroup("/api/admin/menu-groups")
            .WithTags("MenuGroups")
            .RequireAuthorization();

        groups.MapGet("/", async (string? lang, MenuGroupService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListMenuGroups");

        groups.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            MenuGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetMenuGroupById");

        groups.MapPost("/", async (
            CreateMenuGroupRequest request,
            string? lang,
            MenuGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/menu-groups/{response!.Id}", response);
            })
            .WithName("CreateMenuGroup");

        groups.MapPut("/{id:guid}", async (
            Guid id,
            UpdateMenuGroupRequest request,
            string? lang,
            MenuGroupService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateMenuGroup");

        groups.MapDelete("/{id:guid}", async (Guid id, MenuGroupService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteMenuGroup");
    }

    private static void MapMenuItemEndpoints(IEndpointRouteBuilder endpoints)
    {
        var items = endpoints.MapGroup("/api/admin/menu-items")
            .WithTags("MenuItems")
            .RequireAuthorization();

        items.MapGet("/", async (
            Guid groupId,
            string? lang,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var list = await service.GetByGroupAsync(groupId, lang, cancellationToken);
                return Results.Ok(list);
            })
            .WithName("ListMenuItems");

        items.MapGet("/tree", async (
            Guid groupId,
            string? lang,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var (tree, error, statusCode) = await service.GetTreeAsync(groupId, lang, cancellationToken);
                return error is null
                    ? Results.Ok(tree)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetMenuItemTree");

        items.MapPut("/order", async (
            ReorderMenuItemsRequest request,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.ReorderAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(new { ok = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReorderMenuItems");

        items.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetMenuItemById");

        items.MapPost("/", async (
            CreateMenuItemRequest request,
            string? lang,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, lang, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/menu-items/{response!.Id}", response);
            })
            .WithName("CreateMenuItem");

        items.MapPut("/{id:guid}", async (
            Guid id,
            UpdateMenuItemRequest request,
            string? lang,
            MenuItemService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateMenuItem");

        items.MapDelete("/{id:guid}", async (Guid id, MenuItemService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteMenuItem");
    }
}
