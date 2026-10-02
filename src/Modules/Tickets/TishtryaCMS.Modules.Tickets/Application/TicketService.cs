using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Tickets.Domain;
using TishtryaCMS.Modules.Tickets.Infrastructure;

namespace TishtryaCMS.Modules.Tickets.Application;

public sealed class TicketService(TicketsDbContext db)
{
    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> CreateAsync(
        ClaimsPrincipal actor,
        CreateTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return (null, "Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        var category = request.Category?.Trim().ToLowerInvariant();
        if (!TicketCategories.IsKnown(category))
        {
            return (null, "Choose news, event, support, human rights, or other.", StatusCodes.Status400BadRequest);
        }

        try
        {
            var ticket = Ticket.Create(
                userId.Value,
                DisplayName(actor),
                Email(actor),
                category!,
                request.Title,
                request.Body);
            db.Tickets.Add(ticket);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(ticket, []), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<IReadOnlyList<TicketListItemResponse>> ListOwnAsync(
        ClaimsPrincipal actor,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return [];
        }

        var tickets = await db.Tickets.AsNoTracking()
            .Include(x => x.Replies)
            .Where(x => x.UserId == userId.Value)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return tickets.Select(ToListItem).ToList();
    }

    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> GetOwnAsync(
        ClaimsPrincipal actor,
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return (null, "Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        var ticket = await db.Tickets.AsNoTracking()
            .Include(x => x.Replies)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, cancellationToken);

        if (ticket is null)
        {
            return (null, "Request not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(ticket, ticket.Replies), null, StatusCodes.Status200OK);
    }

    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> ReplyOwnAsync(
        ClaimsPrincipal actor,
        Guid id,
        ReplyTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return (null, "Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        var ticket = await db.Tickets
            .Include(x => x.Replies)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId.Value, cancellationToken);

        if (ticket is null)
        {
            return (null, "Request not found.", StatusCodes.Status404NotFound);
        }

        if (ticket.Status == TicketStatuses.Closed)
        {
            return (null, "This request is closed.", StatusCodes.Status400BadRequest);
        }

        try
        {
            var reply = TicketReply.Create(ticket.Id, userId.Value, DisplayName(actor), fromStaff: false, request.Body);
            db.TicketReplies.Add(reply);
            if (ticket.Status == TicketStatuses.Answered)
            {
                ticket.SetStatus(TicketStatuses.Open);
            }
            else
            {
                ticket.Touch();
            }

            await db.SaveChangesAsync(cancellationToken);
            await db.Entry(ticket).Collection(x => x.Replies).LoadAsync(cancellationToken);
            return (ToResponse(ticket, ticket.Replies), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<AdminTicketListResponse> ListAdminAsync(
        string? category,
        string? status,
        CancellationToken cancellationToken)
    {
        var all = await db.Tickets.AsNoTracking()
            .Include(x => x.Replies)
            .ToListAsync(cancellationToken);

        IEnumerable<Ticket> filtered = all;
        if (TicketCategories.IsKnown(category))
        {
            filtered = filtered.Where(x => x.Category == category);
        }

        if (TicketStatuses.IsKnown(status))
        {
            filtered = filtered.Where(x => x.Status == status);
        }

        var items = filtered
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Select(ToListItem)
            .ToList();

        return new AdminTicketListResponse(
            all.Count,
            all.Count(x => x.Status == TicketStatuses.Open),
            all.Count(x => x.Status == TicketStatuses.Answered),
            all.Count(x => x.Status == TicketStatuses.Closed),
            items);
    }

    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> GetAdminAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(x => x.Replies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (ticket is null)
        {
            return (null, "Request not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(ticket, ticket.Replies), null, StatusCodes.Status200OK);
    }

    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> ReplyAdminAsync(
        ClaimsPrincipal actor,
        Guid id,
        ReplyTicketRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(actor);
        if (userId is null)
        {
            return (null, "Unauthorized.", StatusCodes.Status401Unauthorized);
        }

        var ticket = await db.Tickets
            .Include(x => x.Replies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (ticket is null)
        {
            return (null, "Request not found.", StatusCodes.Status404NotFound);
        }

        try
        {
            var reply = TicketReply.Create(ticket.Id, userId.Value, DisplayName(actor), fromStaff: true, request.Body);
            db.TicketReplies.Add(reply);
            ticket.SetStatus(TicketStatuses.Answered);
            await db.SaveChangesAsync(cancellationToken);
            await db.Entry(ticket).Collection(x => x.Replies).LoadAsync(cancellationToken);
            return (ToResponse(ticket, ticket.Replies), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(TicketResponse? Response, string? Error, int StatusCode)> SetStatusAsync(
        Guid id,
        SetTicketStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TicketStatuses.IsKnown(request.Status))
        {
            return (null, "Unknown status.", StatusCodes.Status400BadRequest);
        }

        var ticket = await db.Tickets
            .Include(x => x.Replies)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (ticket is null)
        {
            return (null, "Request not found.", StatusCodes.Status404NotFound);
        }

        ticket.SetStatus(request.Status);
        await db.SaveChangesAsync(cancellationToken);
        return (ToResponse(ticket, ticket.Replies), null, StatusCodes.Status200OK);
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (ticket is null)
        {
            return ("Request not found.", StatusCodes.Status404NotFound);
        }

        db.Tickets.Remove(ticket);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private static TicketListItemResponse ToListItem(Ticket ticket) =>
        new(
            ticket.Id,
            ticket.UserId,
            ticket.AuthorName,
            ticket.AuthorEmail,
            ticket.Category,
            ticket.Title,
            ticket.Status,
            ticket.Replies.Count,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc);

    private static TicketResponse ToResponse(Ticket ticket, IEnumerable<TicketReply> replies) =>
        new(
            ticket.Id,
            ticket.UserId,
            ticket.AuthorName,
            ticket.AuthorEmail,
            ticket.Category,
            ticket.Title,
            ticket.Body,
            ticket.Status,
            ticket.CreatedAtUtc,
            ticket.UpdatedAtUtc,
            replies
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new TicketReplyResponse(
                    x.Id,
                    x.AuthorUserId,
                    x.AuthorName,
                    x.FromStaff,
                    x.Body,
                    x.CreatedAtUtc))
                .ToList());

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string DisplayName(ClaimsPrincipal user)
    {
        var name = user.FindFirstValue("display_name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var email = Email(user);
        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : "User";
    }

    private static string Email(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Email)
        ?? user.FindFirstValue("email")
        ?? "user@local";
}
