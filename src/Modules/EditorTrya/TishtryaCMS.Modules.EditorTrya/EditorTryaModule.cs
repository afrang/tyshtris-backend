using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.EditorTrya.Application;
using TishtryaCMS.Modules.EditorTrya.Application.Registry;
using TishtryaCMS.Modules.EditorTrya.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.EditorTrya;

public sealed class EditorTryaModule : IModule
{
    public string Name => "EditorTrya";

    public void Register(IServiceCollection services)
    {
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<EditorTryaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddSingleton<EditorTryaComponentRegistry>();
        services.AddScoped<EditorTryaService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/editor-trya")
            .WithTags("EditorTrya")
            .RequireAuthorization();

        group.MapGet("/component-types", (EditorTryaService service) =>
            Results.Ok(service.GetComponentTypes()))
            .WithName("ListEditorTryaComponentTypes");

        group.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            string? lang,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetEditorAsync(component, parentId, lang ?? string.Empty, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetEditorTrya");

        group.MapPut("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            string? lang,
            SaveEditorTreeRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.SaveEditorAsync(component, parentId, lang ?? string.Empty, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("SaveEditorTrya");

        group.MapPost("/{component}/{parentId:guid}/containers", async (
            string component,
            Guid parentId,
            string? lang,
            CreateContainerRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateContainerAsync(component, parentId, lang ?? string.Empty, request, cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/editor-trya/containers/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateEditorTryaContainer");

        group.MapPost("/{component}/{parentId:guid}/clone", async (
            string component,
            Guid parentId,
            string? from,
            string? to,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CloneEditorAsync(
                    component,
                    parentId,
                    from ?? string.Empty,
                    to ?? string.Empty,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CloneEditorTrya");

        group.MapPut("/containers/{containerId:guid}", async (
            Guid containerId,
            UpdateContainerRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateContainerAsync(containerId, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateEditorTryaContainer");

        group.MapDelete("/containers/{containerId:guid}", async (
            Guid containerId,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteContainerAsync(containerId, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteEditorTryaContainer");

        group.MapPut("/containers/reorder", async (
            ReorderRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.ReorderContainersAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(new { updated = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReorderEditorTryaContainers");

        group.MapPost("/containers/{containerId:guid}/components", async (
            Guid containerId,
            CreateComponentRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateComponentAsync(containerId, request, cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/editor-trya/components/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateEditorTryaComponent");

        group.MapPut("/components/{componentId:guid}", async (
            Guid componentId,
            UpdateComponentRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateComponentAsync(componentId, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateEditorTryaComponent");

        group.MapDelete("/components/{componentId:guid}", async (
            Guid componentId,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteComponentAsync(componentId, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteEditorTryaComponent");

        group.MapPut("/components/reorder", async (
            ReorderRequest request,
            EditorTryaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.ReorderComponentsAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(new { updated = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReorderEditorTryaComponents");
    }
}
