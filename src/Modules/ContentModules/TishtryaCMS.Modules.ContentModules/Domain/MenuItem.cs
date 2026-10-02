using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.ContentModules.Domain;

public sealed class MenuItem : Entity
{
    public const int MaxDepth = 3;

    public string Title { get; private set; } = string.Empty;
    public string Url { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public Guid GroupId { get; private set; }
    public string? Data { get; private set; }
    public string Function { get; private set; } = MenuLinkFunction.Custom;
    public Guid? TargetId { get; private set; }
    public bool IsMegaMenu { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public MenuGroup? Group { get; private set; }
    public MenuItem? Parent { get; private set; }
    public ICollection<MenuItem> Children { get; private set; } = new List<MenuItem>();
    public ICollection<MenuItemTranslation> Translations { get; private set; } = new List<MenuItemTranslation>();

    private MenuItem()
    {
    }

    public static MenuItem Create(
        string title,
        string url,
        Guid groupId,
        Guid? parentId,
        string? data,
        string function,
        Guid? targetId,
        bool isMegaMenu,
        int sortOrder,
        bool isActive)
    {
        var normalizedFunction = MenuLinkFunction.Normalize(function);
        return new MenuItem
        {
            Id = Guid.Empty,
            Title = NormalizeRequired(title, nameof(title)),
            Url = NormalizeUrl(url),
            GroupId = groupId,
            ParentId = parentId,
            Data = NormalizeOptional(data),
            Function = normalizedFunction,
            TargetId = targetId,
            IsMegaMenu = ResolveMegaMenu(normalizedFunction, isMegaMenu),
            SortOrder = sortOrder,
            IsActive = isActive
        };
    }

    public void Update(
        string title,
        string url,
        Guid? parentId,
        string? data,
        string function,
        Guid? targetId,
        bool isMegaMenu,
        int sortOrder,
        bool isActive)
    {
        var normalizedFunction = MenuLinkFunction.Normalize(function);
        Title = NormalizeRequired(title, nameof(title));
        Url = NormalizeUrl(url);
        ParentId = parentId;
        Data = NormalizeOptional(data);
        Function = normalizedFunction;
        TargetId = targetId;
        IsMegaMenu = ResolveMegaMenu(normalizedFunction, isMegaMenu);
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    public void SetSortOrder(int sortOrder)
    {
        SortOrder = sortOrder;
    }

    private static bool ResolveMegaMenu(string function, bool isMegaMenu) =>
        function.Equals(MenuLinkFunction.GroupBlog, StringComparison.OrdinalIgnoreCase) && isMegaMenu;

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }

    private static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("url is required.", nameof(url));
        }

        return url.Trim();
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
