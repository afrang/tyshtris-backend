namespace TishtryaCMS.Modules.ContentModules.Application;

public sealed record CreateMenuGroupRequest(
    string Title,
    string Key,
    string? Description,
    int SortOrder,
    bool IsActive);

public sealed record UpdateMenuGroupRequest(
    string Title,
    string Key,
    string? Description,
    int SortOrder,
    bool IsActive);

public sealed record MenuGroupResponse(
    Guid Id,
    string Title,
    string Key,
    string? Description,
    int SortOrder,
    bool IsActive,
    string? LanguagePrefix);

public sealed record CreateMenuItemRequest(
    Guid GroupId,
    string Title,
    string Function,
    Guid? TargetId,
    string? Url,
    Guid? ParentId,
    string? Data,
    bool IsMegaMenu,
    int SortOrder,
    bool IsActive);

public sealed record UpdateMenuItemRequest(
    string Title,
    string Function,
    Guid? TargetId,
    string? Url,
    Guid? ParentId,
    string? Data,
    bool IsMegaMenu,
    int SortOrder,
    bool IsActive);

public sealed record MenuItemResponse(
    Guid Id,
    Guid GroupId,
    string Title,
    string Url,
    Guid? ParentId,
    string? Data,
    string Function,
    Guid? TargetId,
    bool IsMegaMenu,
    int SortOrder,
    bool IsActive,
    string? LanguagePrefix);

public sealed record MenuItemTreeResponse(
    Guid Id,
    Guid GroupId,
    string Title,
    string Url,
    Guid? ParentId,
    string? Data,
    string Function,
    Guid? TargetId,
    bool IsMegaMenu,
    int SortOrder,
    bool IsActive,
    string? LanguagePrefix,
    string? ImageUrl,
    IReadOnlyList<MenuItemTreeResponse> Children);

public sealed record MenuItemOrderItem(Guid Id, int SortOrder);

public sealed record ReorderMenuItemsRequest(IReadOnlyList<MenuItemOrderItem> Items);
