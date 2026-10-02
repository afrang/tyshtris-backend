namespace TishtryaCMS.Modules.Comments.Domain;

public static class CommentStatuses
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    public static bool IsValid(string status) =>
        status is Pending or Approved or Rejected;
}
