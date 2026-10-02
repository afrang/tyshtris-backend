using System.Text.Json;

namespace TishtryaCMS.Modules.EditorTrya.Application.Registry;

public sealed class EditorComponentDefinition
{
    public required string Type { get; init; }
    public required string Name { get; init; }
    public required string DefaultDataJson { get; init; }
    public required string DefaultOptionsJson { get; init; }
    public required Func<JsonElement?, string?> ValidateData { get; init; }
    public required Func<JsonElement?, string?> ValidateOptions { get; init; }
    public bool UsesHtml { get; init; }
    public string? FileManagerComponent { get; init; }
}
