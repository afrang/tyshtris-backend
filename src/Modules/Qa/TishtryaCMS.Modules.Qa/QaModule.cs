using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.Qa.Application;
using TishtryaCMS.Modules.Qa.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Qa;

public sealed class QaModule : IModule
{
    public string Name => "Qa";

    public void Register(IServiceCollection services)
    {
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<QaDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<QaService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/qa")
            .WithTags("Qa")
            .RequireAuthorization();

        admin.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            string? lang,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetAsync(
                    component,
                    parentId,
                    lang ?? string.Empty,
                    publishedOnly: false,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetQaAdmin");

        admin.MapPut("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            string? lang,
            SaveQaTreeRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.SaveAsync(
                    component,
                    parentId,
                    lang ?? string.Empty,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("SaveQaAdmin");

        admin.MapPost("/{component}/{parentId:guid}/questions", async (
            string component,
            Guid parentId,
            string? lang,
            CreateQaQuestionRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateQuestionAsync(
                    component,
                    parentId,
                    lang ?? string.Empty,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/qa/questions/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateQaQuestion");

        admin.MapPost("/{component}/{parentId:guid}/clone", async (
            string component,
            Guid parentId,
            string? from,
            string? to,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CloneAsync(
                    component,
                    parentId,
                    from ?? string.Empty,
                    to ?? string.Empty,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CloneQa");

        admin.MapPut("/questions/{questionId:guid}", async (
            Guid questionId,
            string? lang,
            UpdateQaQuestionRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateQuestionAsync(
                    questionId,
                    lang ?? string.Empty,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateQaQuestion");

        admin.MapDelete("/questions/{questionId:guid}", async (
            Guid questionId,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteQuestionAsync(questionId, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteQaQuestion");

        admin.MapPost("/questions/{questionId:guid}/answers", async (
            Guid questionId,
            string? lang,
            CreateQaAnswerRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAnswerAsync(
                    questionId,
                    lang ?? string.Empty,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Created($"/api/admin/qa/answers/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateQaAnswer");

        admin.MapPut("/answers/{answerId:guid}", async (
            Guid answerId,
            string? lang,
            UpdateQaAnswerRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateAnswerAsync(
                    answerId,
                    lang ?? string.Empty,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateQaAnswer");

        admin.MapDelete("/answers/{answerId:guid}", async (
            Guid answerId,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAnswerAsync(answerId, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteQaAnswer");

        admin.MapPut("/questions/reorder", async (
            ReorderRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.ReorderQuestionsAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(new { updated = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReorderQaQuestions");

        admin.MapPut("/answers/reorder", async (
            ReorderRequest request,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.ReorderAnswersAsync(request, cancellationToken);
                return error is null
                    ? Results.Ok(new { updated = true })
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReorderQaAnswers");

        var pub = endpoints.MapGroup("/api/public/qa")
            .WithTags("QaPublic")
            .AllowAnonymous();

        pub.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            string? lang,
            QaService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetAsync(
                    component,
                    parentId,
                    lang ?? string.Empty,
                    publishedOnly: true,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetQaPublic");
    }
}
