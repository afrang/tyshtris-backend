using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Tickets.Domain;

public sealed class Ticket : Entity
{
    public Guid UserId { get; private set; }
    public string AuthorName { get; private set; } = string.Empty;
    public string AuthorEmail { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string Status { get; private set; } = TicketStatuses.Open;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public ICollection<TicketReply> Replies { get; private set; } = new List<TicketReply>();

    private Ticket()
    {
    }

    public static Ticket Create(
        Guid userId,
        string authorName,
        string authorEmail,
        string category,
        string title,
        string body)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId is required.", nameof(userId));
        }

        if (!TicketCategories.IsKnown(category))
        {
            throw new ArgumentException("Unknown category.", nameof(category));
        }

        var now = DateTime.UtcNow;
        return new Ticket
        {
            Id = Guid.Empty,
            UserId = userId,
            AuthorName = Required(authorName, nameof(authorName), 200),
            AuthorEmail = Required(authorEmail, nameof(authorEmail), 256),
            Category = category,
            Title = Required(title, nameof(title), 200),
            Body = Required(body, nameof(body), 4000),
            Status = TicketStatuses.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }

    public void SetStatus(string status)
    {
        if (!TicketStatuses.IsKnown(status))
        {
            throw new ArgumentException("Unknown status.", nameof(status));
        }

        Status = status;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static string Required(string value, string name, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{name} is required.", name);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > max)
        {
            throw new ArgumentException($"{name} is too long.", name);
        }

        return trimmed;
    }
}
