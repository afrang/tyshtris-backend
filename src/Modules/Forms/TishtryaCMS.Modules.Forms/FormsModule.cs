using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.Forms.Application;
using TishtryaCMS.Modules.Forms.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Forms;

public sealed class FormsModule : IModule
{
    public string Name => "Forms";

    public void Register(IServiceCollection services)
    {
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<FormsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<FormService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/forms")
            .WithTags("Forms")
            .RequireAuthorization();

        admin.MapGet("/", async (
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var items = await service.GetAllAsync(http.User, lang, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListForms");

        admin.MapGet("/{id:guid}", async (
            Guid id,
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetByIdAsync(id, http.User, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetFormById");

        admin.MapPost("/", async (
            UpsertFormRequest request,
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(request, http.User, lang, cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/forms/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateForm");

        admin.MapPut("/{id:guid}", async (
            Guid id,
            UpsertFormRequest request,
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAsync(id, request, http.User, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateForm");

        admin.MapDelete("/{id:guid}", async (
            Guid id,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, http.User, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteForm");

        admin.MapPut("/{id:guid}/fields", async (
            Guid id,
            SyncFormFieldsRequest request,
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.SyncFieldsAsync(
                    id,
                    request,
                    http.User,
                    lang,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("SyncFormFields");

        admin.MapGet("/{id:guid}/submissions", async (
            Guid id,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ListSubmissionsAsync(id, http.User, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ListFormSubmissions");

        admin.MapDelete("/{id:guid}/submissions/{submissionId:guid}", async (
            Guid id,
            Guid submissionId,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteSubmissionAsync(
                    id,
                    submissionId,
                    http.User,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteFormSubmission");

        var pub = endpoints.MapGroup("/api/public/forms")
            .WithTags("FormsPublic");

        pub.MapGet("/{slug}", async (
            string slug,
            string? lang,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetPublicBySlugAsync(slug, lang, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .AllowAnonymous()
            .WithName("GetPublicFormBySlug");

        pub.MapPost("/{slug}/submit", async (
            string slug,
            SubmitFormRequest request,
            string? lang,
            HttpContext http,
            FormService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.SubmitPublicAsync(
                    slug,
                    request,
                    lang,
                    http.Connection.RemoteIpAddress?.ToString(),
                    http.Request.Headers.UserAgent.ToString(),
                    cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/forms/{response!.FormId}/submissions", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .AllowAnonymous()
            .WithName("SubmitPublicForm");
    }
}
