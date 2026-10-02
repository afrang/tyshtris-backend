namespace TishtryaCMS.Modules.ContentModules.Domain;

public static class MenuLinkFunction
{
    public const string GroupBlog = "GroupBlog";
    public const string Post = "Post";
    public const string Gallery = "Gallery";
    public const string Form = "Form";
    public const string Custom = "Custom";

    public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
    {
        GroupBlog,
        Post,
        Gallery,
        Form,
        Custom
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("function is required.", nameof(value));
        }

        var normalized = value.Trim();
        if (!All.Contains(normalized))
        {
            throw new ArgumentException(
                "function must be GroupBlog, Post, Gallery, Form, or Custom.",
                nameof(value));
        }

        return All.First(x => x.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
