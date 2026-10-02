using System.Text.Json;

namespace TishtryaCMS.Modules.EditorTrya.Application;

public sealed record EditorComponentTypeResponse(string Type, string Name, JsonElement DefaultData, JsonElement DefaultOptions);

public sealed record EditorComponentResponse(
    Guid Id,
    string Type,
    int Ordered,
    bool Publish,
    JsonElement? Data,
    JsonElement? Options);

public sealed record EditorContainerResponse(
    Guid Id,
    Guid? ParentId,
    string? Component,
    int? Cols,
    int Ordered,
    bool Publish,
    JsonElement? Options,
    IReadOnlyList<EditorComponentResponse> Components);

public sealed record EditorTreeResponse(
    Guid Id,
    string Component,
    Guid ParentId,
    string LanguagePrefix,
    bool Publish,
    IReadOnlyList<EditorContainerResponse> Containers);

public sealed record SaveEditorComponentRequest(
    Guid? Id,
    string Type,
    int Ordered,
    bool Publish,
    JsonElement? Data,
    JsonElement? Options);

public sealed record SaveEditorContainerRequest(
    Guid? Id,
    Guid? ParentId,
    string? Component,
    int? Cols,
    int Ordered,
    bool Publish,
    JsonElement? Options,
    IReadOnlyList<SaveEditorComponentRequest>? Components);

public sealed record SaveEditorTreeRequest(
    bool? Publish,
    IReadOnlyList<SaveEditorContainerRequest> Containers);

public sealed record CreateContainerRequest(
    Guid? ParentId,
    string? Component,
    int? Cols,
    int? Ordered,
    bool Publish,
    JsonElement? Options);

public sealed record UpdateContainerRequest(
    Guid? ParentId,
    string? Component,
    int? Cols,
    int Ordered,
    bool Publish,
    JsonElement? Options);

public sealed record CreateComponentRequest(
    string Type,
    int? Ordered,
    bool Publish,
    JsonElement? Data,
    JsonElement? Options);

public sealed record UpdateComponentRequest(
    string Type,
    int Ordered,
    bool Publish,
    JsonElement? Data,
    JsonElement? Options);

public sealed record ReorderItemRequest(Guid Id, int Ordered, Guid? ContainerId);

public sealed record ReorderRequest(IReadOnlyList<ReorderItemRequest> Items);
