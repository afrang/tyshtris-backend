namespace TishtryaCMS.Modules.Comments.Application;

public sealed record CommentResponse(
    Guid Id,
    string Component,
    Guid ParentId,
    Guid? ParentCommentId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorEmail,
    string Body,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<CommentResponse> Replies);

public sealed record CreateCommentRequest(
    string Body,
    Guid? ParentCommentId,
    string? AuthorDisplayName = null,
    string? AuthorEmail = null,
    string? CaptchaToken = null);

public sealed record UpdateCommentRequest(string Body);

public sealed record UpdateCommentStatusRequest(string Status);

public sealed record CommentListResponse(
    string Component,
    Guid ParentId,
    bool CommentsEnabled,
    IReadOnlyList<CommentResponse> Comments);

public sealed record AdminCommentListItem(
    Guid Id,
    string Component,
    Guid ParentId,
    string? ParentTitle,
    Guid? ParentCommentId,
    Guid UserId,
    string AuthorDisplayName,
    string? AuthorEmail,
    string Body,
    string Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record AdminCommentListResponse(
    int Total,
    int PendingCount,
    int ApprovedCount,
    int RejectedCount,
    IReadOnlyList<AdminCommentListItem> Items);
