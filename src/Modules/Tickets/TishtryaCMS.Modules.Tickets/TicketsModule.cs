using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TishtryaCMS.Modules.Tickets.Application;
using TishtryaCMS.Modules.Tickets.Infrastructure;
using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Tickets;

public sealed class TicketsModule : IModule
{
    public string Name => "Tickets";

    public void Register(IServiceCollection services)
    {
    }

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is missing.");

        services.AddDbContext<TicketsDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<TicketService>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var mine = endpoints.MapGroup("/api/tickets")
            .WithTags("Tickets")
            .RequireAuthorization();

        mine.MapGet("/", async (HttpContext http, TicketService service, CancellationToken cancellationToken) =>
            {
                var items = await service.ListOwnAsync(http.User, cancellationToken);
                return Results.Ok(items);
            })
            .WithName("ListOwnTickets");

        mine.MapGet("/{id:guid}", async (Guid id, HttpContext http, TicketService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetOwnAsync(http.User, id, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetOwnTicket");

        mine.MapPost("/", async (CreateTicketRequest request, HttpContext http, TicketService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.CreateAsync(http.User, request, cancellationToken);
                return error is null
                    ? Results.Created($"/api/tickets/{response!.Id}", response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("CreateTicket");

        mine.MapPost("/{id:guid}/replies", async (
            Guid id,
            ReplyTicketRequest request,
            HttpContext http,
            TicketService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ReplyOwnAsync(http.User, id, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReplyOwnTicket");

        var admin = endpoints.MapGroup("/api/admin/tickets")
            .WithTags("TicketsAdmin")
            .RequireAuthorization("AdminOnly");

        admin.MapGet("/", async (string? category, string? status, TicketService service, CancellationToken cancellationToken) =>
            {
                var response = await service.ListAdminAsync(category, status, cancellationToken);
                return Results.Ok(response);
            })
            .WithName("ListTicketsAdmin");

        admin.MapGet("/{id:guid}", async (Guid id, TicketService service, CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.GetAdminAsync(id, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("GetTicketAdmin");

        admin.MapPost("/{id:guid}/replies", async (
            Guid id,
            ReplyTicketRequest request,
            HttpContext http,
            TicketService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.ReplyAdminAsync(http.User, id, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("ReplyTicketAdmin");

        admin.MapPut("/{id:guid}/status", async (
            Guid id,
            SetTicketStatusRequest request,
            TicketService service,
            CancellationToken cancellationToken) =>
            {
                var (response, error, statusCode) = await service.SetStatusAsync(id, request, cancellationToken);
                return error is null
                    ? Results.Ok(response)
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("SetTicketStatus");

        admin.MapDelete("/{id:guid}", async (Guid id, TicketService service, CancellationToken cancellationToken) =>
            {
                var (error, statusCode) = await service.DeleteAsync(id, cancellationToken);
                return error is null
                    ? Results.NoContent()
                    : Results.Json(new { error }, statusCode: statusCode);
            })
            .WithName("DeleteTicket");
    }
}
