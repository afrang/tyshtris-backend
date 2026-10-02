namespace TishtryaCMS.Modules.Tickets.Application;

public sealed record CreateTicketRequest(string Category, string Title, string Body);

public sealed record ReplyTicketRequest(string Body);

public sealed record SetTicketStatusRequest(string Status);

public sealed record TicketReplyResponse(
    Guid Id,
    Guid AuthorUserId,
    string AuthorName,
    bool FromStaff,
    string Body,
    DateTime CreatedAtUtc);

public sealed record TicketResponse(
    Guid Id,
    Guid UserId,
    string AuthorName,
    string AuthorEmail,
    string Category,
    string Title,
    string Body,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<TicketReplyResponse> Replies);

public sealed record TicketListItemResponse(
    Guid Id,
    Guid UserId,
    string AuthorName,
    string AuthorEmail,
    string Category,
    string Title,
    string Status,
    int ReplyCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AdminTicketListResponse(
    int Total,
    int OpenCount,
    int AnsweredCount,
    int ClosedCount,
    IReadOnlyList<TicketListItemResponse> Items);
