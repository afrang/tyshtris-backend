using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class MenuGroupTranslation : Entity
{
    public Guid GroupId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public MenuGroup? Group { get; private set; }

    private MenuGroupTranslation()
    {
    }

    public static MenuGroupTranslation Create(
        Guid groupId,
        string languagePrefix,
        string title,
        string? description)
    {
        return new MenuGroupTranslation
        {
            Id = Guid.Empty,
            GroupId = groupId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            Title = NormalizeRequired(title, nameof(title)),
            Description = NormalizeOptional(description)
        };
    }

    public void Update(string title, string? description)
    {
        Title = NormalizeRequired(title, nameof(title));
        Description = NormalizeOptional(description);
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("languagePrefix is required.", nameof(prefix));
        }

        return prefix.Trim().ToLowerInvariant().Replace('_', '-');
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
