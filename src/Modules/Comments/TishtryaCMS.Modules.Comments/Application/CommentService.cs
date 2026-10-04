using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Comments.Domain;
using TishtryaCMS.Modules.Comments.Infrastructure;
using TishtryaCMS.Modules.ContentModules.Infrastructure;
using TishtryaCMS.SharedKernel.Turnstile;

namespace TishtryaCMS.Modules.Comments.Application;

public sealed class CommentService(
    CommentsDbContext db,
    ContentModulesDbContext contentDb,
    ITurnstileValidator turnstile)
{
    public static readonly Guid GuestUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public async Task<(CommentListResponse? Response, string? Error, int StatusCode)> ListAsync(
        string component,
        Guid parentId,
        bool approvedOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalized = Comment.NormalizeComponent(component);
            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var enabled = await AreCommentsEnabledAsync(normalized, parentId, cancellationToken);
            var query = db.Comments.AsNoTracking()
                .Where(x => x.Component == normalized && x.ParentId == parentId);

            if (approvedOnly)
            {
                query = query.Where(x => x.Status == CommentStatuses.Approved);
            }

            var flat = await query
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

            return (new CommentListResponse(normalized, parentId, enabled, BuildTree(flat)), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<AdminCommentListResponse> ListAllAsync(
        string? status,
        string? component,
        string? query,
        CancellationToken cancellationToken)
    {
        var commentsQuery = db.Comments.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToLowerInvariant();
            if (CommentStatuses.IsValid(normalizedStatus))
            {
                commentsQuery = commentsQuery.Where(x => x.Status == normalizedStatus);
            }
        }

        if (!string.IsNullOrWhiteSpace(component))
        {
            var normalizedComponent = Comment.NormalizeComponent(component);
            commentsQuery = commentsQuery.Where(x => x.Component == normalizedComponent);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim().ToLowerInvariant();
            commentsQuery = commentsQuery.Where(x =>
                x.Body.ToLower().Contains(q)
                || x.AuthorDisplayName.ToLower().Contains(q)
                || (x.AuthorEmail != null && x.AuthorEmail.ToLower().Contains(q)));
        }

        var comments = await commentsQuery
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        var blogPostIds = comments
            .Where(x => x.Component == "blogpost")
            .Select(x => x.ParentId)
            .Distinct()
            .ToList();

        var postTitles = blogPostIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await contentDb.BlogPosts.AsNoTracking()
                .Where(x => blogPostIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Title, cancellationToken);

        var items = comments.Select(comment =>
        {
            string? parentTitle = null;
            if (comment.Component == "blogpost")
            {
                postTitles.TryGetValue(comment.ParentId, out parentTitle);
            }

            return new AdminCommentListItem(
                comment.Id,
                comment.Component,
                comment.ParentId,
                parentTitle,
                comment.ParentCommentId,
                comment.UserId,
                comment.AuthorDisplayName,
                comment.AuthorEmail,
                comment.Body,
                comment.Status,
                comment.CreatedAt,
                comment.UpdatedAt);
        }).ToList();

        // Counts across the full table (not only filtered), for dashboard chips.
        var allStatuses = await db.Comments.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int CountOf(string s) => allStatuses.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        return new AdminCommentListResponse(
            items.Count,
            CountOf(CommentStatuses.Pending),
            CountOf(CommentStatuses.Approved),
            CountOf(CommentStatuses.Rejected),
            items);
    }

    public async Task<(CommentResponse? Response, string? Error, int StatusCode)> CreateAsync(
        string component,
        Guid parentId,
        CreateCommentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            var captchaError = await turnstile.ValidateAsync(request.CaptchaToken, cancellationToken);
            if (captchaError is not null)
            {
                return (null, captchaError, StatusCodes.Status400BadRequest);
            }

            var normalized = Comment.NormalizeComponent(component);
            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var userId = TryGetUserId(user);
            string displayName;
            string? email;

            if (userId is not null)
            {
                displayName = user.FindFirstValue("display_name")
                    ?? user.FindFirstValue(ClaimTypes.Email)
                    ?? user.Identity?.Name
                    ?? "User";
                email = user.FindFirstValue(ClaimTypes.Email)
                    ?? user.FindFirstValue("email");
            }
            else
            {
                if (string.IsNullOrWhiteSpace(request.AuthorDisplayName))
                {
                    return (null, "Author name is required.", StatusCodes.Status400BadRequest);
                }

                userId = GuestUserId;
                displayName = request.AuthorDisplayName.Trim();
                email = string.IsNullOrWhiteSpace(request.AuthorEmail)
                    ? null
                    : request.AuthorEmail.Trim();
            }

            var enabled = await AreCommentsEnabledAsync(normalized, parentId, cancellationToken);
            if (!enabled)
            {
                return (null, "Comments are disabled for this item.", StatusCodes.Status403Forbidden);
            }

            if (request.ParentCommentId.HasValue)
            {
                var parent = await db.Comments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == request.ParentCommentId.Value, cancellationToken);
                if (parent is null
                    || parent.Component != normalized
                    || parent.ParentId != parentId)
                {
                    return (null, "Parent comment was not found.", StatusCodes.Status400BadRequest);
                }
            }

            var comment = Comment.Create(
                normalized,
                parentId,
                userId.Value,
                displayName,
                email,
                request.Body,
                request.ParentCommentId,
                CommentStatuses.Pending);

            db.Comments.Add(comment);
            await db.SaveChangesAsync(cancellationToken);

            return (ToResponse(comment, []), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(CommentResponse? Response, string? Error, int StatusCode)> UpdateOwnAsync(
        Guid commentId,
        UpdateCommentRequest request,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = TryGetUserId(user);
            if (userId is null)
            {
                return (null, "Authentication required.", StatusCodes.Status401Unauthorized);
            }

            var comment = await db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);
            if (comment is null)
            {
                return (null, "Comment not found.", StatusCodes.Status404NotFound);
            }

            if (comment.UserId != userId.Value && !IsAdmin(user))
            {
                return (null, "You can only edit your own comments.", StatusCodes.Status403Forbidden);
            }

            comment.UpdateBody(request.Body);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(comment, []), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(CommentResponse? Response, string? Error, int StatusCode)> UpdateStatusAsync(
        Guid commentId,
        UpdateCommentStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var comment = await db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);
            if (comment is null)
            {
                return (null, "Comment not found.", StatusCodes.Status404NotFound);
            }

            comment.SetStatus(request.Status);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(comment, []), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(
        Guid commentId,
        ClaimsPrincipal? user,
        bool adminOverride,
        CancellationToken cancellationToken)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(x => x.Id == commentId, cancellationToken);
        if (comment is null)
        {
            return ("Comment not found.", StatusCodes.Status404NotFound);
        }

        if (!adminOverride)
        {
            var userId = user is null ? null : TryGetUserId(user);
            if (userId is null)
            {
                return ("Authentication required.", StatusCodes.Status401Unauthorized);
            }

            if (comment.UserId != userId.Value && !IsAdmin(user!))
            {
                return ("You can only delete your own comments.", StatusCodes.Status403Forbidden);
            }
        }

        var hasReplies = await db.Comments.AnyAsync(x => x.ParentCommentId == commentId, cancellationToken);
        if (hasReplies)
        {
            // Soft-disable by rejecting so reply tree stays intact.
            comment.SetStatus(CommentStatuses.Rejected);
            comment.UpdateBody("[deleted]");
        }
        else
        {
            db.Comments.Remove(comment);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    private async Task<bool> AreCommentsEnabledAsync(
        string component,
        Guid parentId,
        CancellationToken cancellationToken)
    {
        if (component is not "blogpost")
        {
            // Other feature apps can add their own gates later; default allow.
            return true;
        }

        var post = await contentDb.BlogPosts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == parentId, cancellationToken);

        return post?.CommentsEnabled ?? false;
    }

    private static IReadOnlyList<CommentResponse> BuildTree(IReadOnlyList<Comment> flat)
    {
        var lookup = flat.ToDictionary(x => x.Id, x => new List<Comment>());
        var roots = new List<Comment>();

        foreach (var comment in flat)
        {
            if (comment.ParentCommentId is Guid parentId && lookup.ContainsKey(parentId))
            {
                lookup[parentId].Add(comment);
            }
            else
            {
                roots.Add(comment);
            }
        }

        CommentResponse Map(Comment comment) =>
            ToResponse(comment, lookup.TryGetValue(comment.Id, out var children)
                ? children.OrderBy(c => c.CreatedAt).Select(Map).ToList()
                : []);

        return roots.OrderBy(x => x.CreatedAt).Select(Map).ToList();
    }

    private static CommentResponse ToResponse(Comment comment, IReadOnlyList<CommentResponse> replies) =>
        new(
            comment.Id,
            comment.Component,
            comment.ParentId,
            comment.ParentCommentId,
            comment.UserId,
            comment.AuthorDisplayName,
            comment.AuthorEmail,
            comment.Body,
            comment.Status,
            comment.CreatedAt,
            comment.UpdatedAt,
            replies);

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static bool IsAdmin(ClaimsPrincipal principal) =>
        string.Equals(principal.FindFirstValue(ClaimTypes.Role), "admin", StringComparison.OrdinalIgnoreCase);
}
