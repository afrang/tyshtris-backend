using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Comments.Domain;

public sealed class Comment : Entity
{
    public string Component { get; private set; } = string.Empty;
    public Guid ParentId { get; private set; }
    public Guid? ParentCommentId { get; private set; }
    public Guid UserId { get; private set; }
    public string AuthorDisplayName { get; private set; } = string.Empty;
    public string? AuthorEmail { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string Status { get; private set; } = CommentStatuses.Pending;
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public Comment? ParentComment { get; private set; }
    public ICollection<Comment> Replies { get; private set; } = new List<Comment>();

    private Comment()
    {
    }

    public static Comment Create(
        string component,
        Guid parentId,
        Guid userId,
        string authorDisplayName,
        string? authorEmail,
        string body,
        Guid? parentCommentId = null,
        string? status = null)
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("parentId is required.", nameof(parentId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("userId is required.", nameof(userId));
        }

        var now = DateTime.UtcNow;
        return new Comment
        {
            Id = Guid.Empty,
            Component = NormalizeComponent(component),
            ParentId = parentId,
            ParentCommentId = parentCommentId,
            UserId = userId,
            AuthorDisplayName = NormalizeRequired(authorDisplayName, nameof(authorDisplayName)),
            AuthorEmail = NormalizeOptional(authorEmail),
            Body = NormalizeRequired(body, nameof(body)),
            Status = NormalizeStatus(status),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void UpdateBody(string body)
    {
        Body = NormalizeRequired(body, nameof(body));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStatus(string status)
    {
        Status = NormalizeStatus(status);
        UpdatedAt = DateTime.UtcNow;
    }

    public static string NormalizeComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("component is required.", nameof(component));
        }

        return component.Trim().ToLowerInvariant();
    }

    private static string NormalizeStatus(string? status)
    {
        var value = string.IsNullOrWhiteSpace(status)
            ? CommentStatuses.Pending
            : status.Trim().ToLowerInvariant();

        if (!CommentStatuses.IsValid(value))
        {
            throw new ArgumentException("Status must be 'pending', 'approved', or 'rejected'.", nameof(status));
        }

        return value;
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
