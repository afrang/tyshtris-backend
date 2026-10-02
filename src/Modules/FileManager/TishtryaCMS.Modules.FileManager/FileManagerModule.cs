using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using TishtryaCMS.Modules.FileManager.Application;
using TishtryaCMS.Modules.FileManager.Infrastructure;
using TishtryaCMS.Modules.FileManager.Options;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.FileManager;

public sealed class FileManagerModule : IModule
{
    public string Name => "FileManager";

    public void Register(IServiceCollection services)
    {
        // Registered via Register(services, configuration) from the host.
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileManagerOptions>(configuration.GetSection(FileManagerOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<FileManagerDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<FileManagerService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/file-manager")
            .WithTags("FileManager")
            .RequireAuthorization()
            .DisableAntiforgery();

        group.MapPost("/upload", async (
            HttpRequest request,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                if (!request.HasFormContentType)
                {
                    return Results.Json(new { error = "multipart/form-data is required." }, statusCode: 400);
                }

                var form = await request.ReadFormAsync(cancellationToken);
                var component = form["component"].ToString();
                var parentIdRaw = form["parent_id"].ToString();
                var orderedRaw = form["ordered"].ToString();
                var file = form.Files.GetFile("file");

                Guid? parentId = null;
                if (!string.IsNullOrWhiteSpace(parentIdRaw))
                {
                    if (!Guid.TryParse(parentIdRaw, out var parsedParent))
                    {
                        return Results.Json(new { error = "parent_id must be a valid GUID." }, statusCode: 400);
                    }

                    parentId = parsedParent;
                }

                int? ordered = null;
                if (!string.IsNullOrWhiteSpace(orderedRaw))
                {
                    if (!int.TryParse(orderedRaw, out var parsedOrder))
                    {
                        return Results.Json(new { error = "ordered must be an integer." }, statusCode: 400);
                    }

                    ordered = parsedOrder;
                }

                if (file is null)
                {
                    return Results.Json(new { error = "file is required." }, statusCode: 400);
                }

                var (response, error, statusCode) = await service.UploadAsync(
                    component,
                    parentId,
                    file,
                    ordered,
                    cancellationToken);

                return error is null
                    ? Results.Created($"/api/admin/file-manager/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UploadFileManagerFile")
            .DisableAntiforgery();

        group.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var items = await service.GetFilesAsync(component, parentId, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("GetFileManagerFiles");

        group.MapDelete("/{id:guid}", async (
            Guid id,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteFileManagerFile");

        group.MapPut("/order", async (
            List<UpdateFileOrderItem> items,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.UpdateOrderAsync(items, cancellationToken);
                return error is null
                    ? Results.Ok(new { updated = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateFileManagerOrder");

        group.MapPost("/uploads/init", async (
            InitChunkUploadRequest request,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.InitChunkUploadAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("InitChunkUpload");

        group.MapPut("/uploads/{uploadId:guid}/chunks/{chunkIndex:int}", async (
            Guid uploadId,
            int chunkIndex,
            HttpRequest request,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.UploadChunkAsync(
                    uploadId,
                    chunkIndex,
                    request.Body,
                    request.ContentLength,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UploadFileChunk");

        group.MapPost("/uploads/complete", async (
            CompleteChunkUploadRequest request,
            FileManagerService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CompleteChunkUploadAsync(
                    request.UploadId,
                    cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/file-manager/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CompleteChunkUpload");
    }

    public static void UseStaticUploads(WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<FileManagerOptions>>().Value;
        var storageRoot = Path.IsPathRooted(options.StorageRoot)
            ? options.StorageRoot
            : Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, options.StorageRoot));

        Directory.CreateDirectory(storageRoot);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storageRoot),
            RequestPath = options.PublicPathPrefix.TrimEnd('/')
        });
    }
}
