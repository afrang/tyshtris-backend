using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class MenuGroup : Entity
{
    public string Title { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public ICollection<MenuItem> Items { get; private set; } = new List<MenuItem>();
    public ICollection<MenuGroupTranslation> Translations { get; private set; } = new List<MenuGroupTranslation>();

    private MenuGroup()
    {
    }

    public static MenuGroup Create(
        string title,
        string key,
        string? description,
        int sortOrder,
        bool isActive)
    {
        return new MenuGroup
        {
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Key = NormalizeKey(key),
            Description = NormalizeOptional(description),
            SortOrder = sortOrder,
            IsActive = isActive
        };
    }

    public void Update(
        string title,
        string key,
        string? description,
        int sortOrder,
        bool isActive)
    {
        Title = NormalizeRequired(title, nameof(title));
        Key = NormalizeKey(key);
        Description = NormalizeOptional(description);
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string NormalizeKey(string key)
    {
        var normalized = NormalizeRequired(key, nameof(key)).ToLowerInvariant();
        return normalized.Replace(' ', '-');
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }
}
