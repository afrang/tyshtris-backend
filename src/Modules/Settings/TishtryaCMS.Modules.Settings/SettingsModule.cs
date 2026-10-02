using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.Settings.Application;
using TishtryaCMS.Modules.Settings.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Settings;

public sealed class SettingsModule : IModule
{
    public string Name => "Settings";

    public void Register(IServiceCollection services)
    {
        // Registered via Register(services, configuration) from the host.
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<SettingsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<SettingsService>();
        services.AddScoped<LanguageService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        MapSiteSettingsEndpoints(endpoints);
        MapLanguageEndpoints(endpoints);
        MapPublicEndpoints(endpoints);
    }

    private static void MapPublicEndpoints(IEndpointRouteBuilder endpoints)
    {
        var pub = endpoints.MapGroup("/api/public")
            .WithTags("Public")
            .AllowAnonymous();

        pub.MapGet("/settings", async (
            string? lang,
            SettingsService service,
            CancellationToken cancellationToken) =>
            {
                var response = await service.GetPublicAsync(lang, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetPublicSiteSettings");

        pub.MapGet("/languages", async (LanguageService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListPublicLanguages");
    }

    private static void MapSiteSettingsEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/settings")
            .WithTags("Settings")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapGet("/", async (
            string? lang,
            SettingsService service,
            CancellationToken cancellationToken) =>
            {
                var response = await service.GetAsync(lang, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("GetSiteSettings");

        group.MapPut("/", async (
            UpdateAllSettingsRequest request,
            string? lang,
            SettingsService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAllAsync(request, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateSiteSettings");

        group.MapPost("/logo", async (
            HttpRequest request,
            SettingsService service,
            CancellationToken cancellationToken) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.Json(new { error = "multipart/form-data is required." }, statusCode: 400);
                }

                var form = await request.ReadFormAsync(cancellationToken);
                var file = form.Files.GetFile("file");
                if (file is null)
                {
                    return Results.Json(new { error = "file is required." }, statusCode: 400);
                }

                var (response, error, statusCode) = await service.UploadLogoAsync(file, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UploadSiteLogo");
    }

    private static void MapLanguageEndpoints(IEndpointRouteBuilder endpoints)
    {
        var languages = endpoints.MapGroup("/api/admin/languages")
            .WithTags("Languages")
            .RequireAuthorization();

        languages.MapGet("/", async (LanguageService service, CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListLanguages");

        languages.MapGet("/{id:guid}", async (Guid id, LanguageService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetLanguageById");

        languages.MapPost("/", async (CreateLanguageRequest request, LanguageService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, cancellationToken);
                if (error is not null)
                {
                    return Results.Json(new { error }, statusCode: statusCode);
                }

                return Results.Created($"/api/admin/languages/{response!.Id}", response);
            })
            .WithName("CreateLanguage");

        languages.MapPut("/{id:guid}", async (
            Guid id,
            UpdateLanguageRequest request,
            LanguageService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateLanguage");

        languages.MapDelete("/{id:guid}", async (Guid id, LanguageService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteLanguage");
    }
}
