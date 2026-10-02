using System.Text.Json;
using TishtryaCMS.Modules.EditorTrya.Application;
using TishtryaCMS.Modules.FileManager.Application;

namespace TishtryaCMS.Api;

internal static class PublicEditorMediaMap
{
    public static async Task<IReadOnlyDictionary<string, string>> BuildAsync(
        EditorTreeResponse? tree,
        FileManagerService fileManagerService,
        CancellationToken cancellationToken)
    {
        if (tree is null)
        {
            return new Dictionary<string, string>();
        }

        var fileIds = new HashSet<Guid>();
        var imageComponentIds = new List<Guid>();
        var galleryComponentIds = new List<Guid>();
        var mediaComponentIds = new List<Guid>();

        foreach (var container in tree.Containers)
        {
            foreach (var component in container.Components)
            {
                if (component.Data is { } data)
                {
                    if (data.ValueKind == JsonValueKind.Object
                        && data.TryGetProperty("fileId", out var fileIdProp)
                        && fileIdProp.ValueKind == JsonValueKind.String
                        && Guid.TryParse(fileIdProp.GetString(), out var fileId))
                    {
                        fileIds.Add(fileId);
                    }
                }

                switch (component.Type.ToLowerInvariant())
                {
                    case "image":
                        imageComponentIds.Add(component.Id);
                        break;
                    case "gallery":
                        galleryComponentIds.Add(component.Id);
                        break;
                    case "video":
                        mediaComponentIds.Add(component.Id);
                        break;
                }
            }
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in await fileManagerService.GetUrlsByIdsAsync(fileIds, cancellationToken))
        {
            map[pair.Key.ToString()] = pair.Value;
        }

        foreach (var file in await fileManagerService.GetFilesByParentIdsAsync(
                     "editortryaimage",
                     imageComponentIds,
                     cancellationToken))
        {
            map[file.Id.ToString()] = file.FullAddress;
        }

        foreach (var file in await fileManagerService.GetFilesByParentIdsAsync(
                     "editortryagallery",
                     galleryComponentIds,
                     cancellationToken))
        {
            map[file.Id.ToString()] = file.FullAddress;
        }

        foreach (var file in await fileManagerService.GetFilesByParentIdsAsync(
                     "editortryavideoaudio",
                     mediaComponentIds,
                     cancellationToken))
        {
            map[file.Id.ToString()] = file.FullAddress;
        }

        return map;
    }
}
