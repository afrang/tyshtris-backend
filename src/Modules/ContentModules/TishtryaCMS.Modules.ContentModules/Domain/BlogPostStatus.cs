namespace TishtryaCMS.Modules.ContentModules.Domain;

public static class BlogPostStatus
{
    public const string Draft = "draft";
    public const string Published = "published";

    public static bool IsValid(string? status) =>
        status is Draft or Published;
}
