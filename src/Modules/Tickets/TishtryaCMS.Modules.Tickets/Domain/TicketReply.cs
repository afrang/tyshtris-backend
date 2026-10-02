using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Tickets.Domain;

public sealed class TicketReply : Entity
{
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public bool FromStaff { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }

    public Ticket? Ticket { get; private set; }

    private TicketReply()
    {
    }

    public static TicketReply Create(
        Guid ticketId,
        Guid authorUserId,
        string authorName,
        bool fromStaff,
        string body)
    {
        if (ticketId == Guid.Empty)
        {
            throw new ArgumentException("ticketId is required.", nameof(ticketId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("authorUserId is required.", nameof(authorUserId));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("body is required.", nameof(body));
        }

        var trimmed = body.Trim();
        if (trimmed.Length > 4000)
        {
            throw new ArgumentException("body is too long.", nameof(body));
        }

        var name = string.IsNullOrWhiteSpace(authorName) ? "User" : authorName.Trim();
        if (name.Length > 200)
        {
            name = name[..200];
        }

        return new TicketReply
        {
            Id = Guid.Empty,
            TicketId = ticketId,
            AuthorUserId = authorUserId,
            AuthorName = name,
            FromStaff = fromStaff,
            Body = trimmed,
            CreatedAtUtc = DateTime.UtcNow
        };
    }
}
