using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.Comments.Application;
using TishtryaCMS.Modules.Comments.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Comments;

public sealed class CommentsModule : IModule
{
    public string Name => "Comments";

    public void Register(IServiceCollection services)
    {
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<CommentsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<CommentService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/comments")
            .WithTags("Comments")
            .RequireAuthorization();

        admin.MapGet("/", async (
            string? status,
            string? component,
            string? q,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var response = await service.ListAllAsync(status, component, q, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListAllCommentsAdmin");

        admin.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ListAsync(
                    component,
                    parentId,
                    approvedOnly: false,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ListCommentsAdmin");

        admin.MapPut("/{commentId:guid}/status", async (
            Guid commentId,
            UpdateCommentStatusRequest request,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateStatusAsync(
                    commentId,
                    request,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("UpdateCommentStatusAdmin");

        admin.MapDelete("/{commentId:guid}", async (
            Guid commentId,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(
                    commentId,
                    user: null,
                    adminOverride: true,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteCommentAdmin");

        var pub = endpoints.MapGroup("/api/public/comments")
            .WithTags("CommentsPublic");

        pub.MapGet("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ListAsync(
                    component,
                    parentId,
                    approvedOnly: true,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .AllowAnonymous()
            .WithName("ListCommentsPublic");

        pub.MapPost("/{component}/{parentId:guid}", async (
            string component,
            Guid parentId,
            CreateCommentRequest request,
            ClaimsPrincipal user,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(
                    component,
                    parentId,
                    request,
                    user,
                    cancellationToken);
                return error is null
                    ? Results.Created($"/api/public/comments/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .AllowAnonymous()
            .WithName("CreateCommentPublic");

        pub.MapPut("/{commentId:guid}", async (
            Guid commentId,
            UpdateCommentRequest request,
            ClaimsPrincipal user,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.UpdateOwnAsync(
                    commentId,
                    request,
                    user,
                    cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .RequireAuthorization()
            .WithName("UpdateCommentPublic");

        pub.MapDelete("/{commentId:guid}", async (
            Guid commentId,
            ClaimsPrincipal user,
            CommentService service,
            CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(
                    commentId,
                    user,
                    adminOverride: false,
                    cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .RequireAuthorization()
            .WithName("DeleteCommentPublic");
    }
}
