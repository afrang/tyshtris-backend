using TishtryaCMS.Modules.ContentModules.Application;
using TishtryaCMS.Modules.Settings.Application;

namespace TishtryaCMS.Api;

public static class HeaderEndpoints
{
    public static void MapHeaderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/header", async (
            string? lang,
            SettingsService settingsService,
            LanguageService languageService,
            MenuItemService menuItemService,
            CancellationToken cancellationToken) =>
            {
                var settings = await settingsService.GetPublicAsync(lang, cancellationToken);
                var languages = await languageService.GetAllAsync(cancellationToken);
                var (menu, _, _) = await menuItemService.GetPublicTreeByKeyAsync(
                    "topmenu",
                    lang,
                    cancellationToken);

                return Results.Ok(new HeaderResponse(
                    settings,
                    languages,
                    menu ?? []));
            })
            .AllowAnonymous()
            .WithName("GetHeader")
            .WithTags("Header");
    }
}

public sealed record HeaderResponse(
    PublicSiteSettingsResponse Settings,
    IReadOnlyList<LanguageResponse> Languages,
    IReadOnlyList<MenuItemTreeResponse> Menu);
