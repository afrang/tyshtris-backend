using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.EditorTrya.Application.Registry;
using TishtryaCMS.Modules.EditorTrya.Domain;
using TishtryaCMS.Modules.EditorTrya.Infrastructure;
using TishtryaCMS.Modules.FileManager.Application;
using TishtryaCMS.Modules.FileManager.Infrastructure;

namespace TishtryaCMS.Modules.EditorTrya.Application;

public sealed class EditorTryaService(
    EditorTryaDbContext db,
    FileManagerDbContext fileDb,
    FileManagerService fileManagerService,
    EditorTryaComponentRegistry registry)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public IReadOnlyList<EditorComponentTypeResponse> GetComponentTypes()
    {
        return registry.All
            .OrderBy(x => x.Name)
            .Select(x => new EditorComponentTypeResponse(
                x.Type,
                x.Name,
                ParseJsonRequired(x.DefaultDataJson),
                ParseJsonRequired(x.DefaultOptionsJson)))
            .ToList();
    }

    public async Task<(EditorTreeResponse? Response, string? Error, int StatusCode)> GetPublishedEditorAsync(
        string component,
        Guid parentId,
        string lang,
        CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(lang))
            {
                return (null, "lang is required.", StatusCodes.Status400BadRequest);
            }

            var prefix = lang.Trim().ToLowerInvariant().Replace('_', '-');
            var normalizedComponent = EditorTryaContent.Create(component, parentId, prefix).Component;

            var content = await db.Contents
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Component == normalizedComponent
                         && x.ParentId == parentId
                         && x.LanguagePrefix == prefix
                         && x.Publish,
                    cancellationToken);

            if (content is null)
            {
                return (null, null, StatusCodes.Status200OK);
            }

            var tree = await BuildPublishedTreeAsync(content.Id, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(EditorTreeResponse? Response, string? Error, int StatusCode)> GetEditorAsync(
        string component,
        Guid parentId,
        string lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await EnsureContentAsync(component, parentId, lang, cancellationToken);
            var tree = await BuildTreeAsync(content.Id, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(EditorTreeResponse? Response, string? Error, int StatusCode)> SaveEditorAsync(
        string component,
        Guid parentId,
        string lang,
        SaveEditorTreeRequest request,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var content = await EnsureContentAsync(component, parentId, lang, cancellationToken);
            if (request.Publish.HasValue)
            {
                content.SetPublish(request.Publish.Value);
            }
            else
            {
                content.Touch();
            }

            var existingContainers = await db.Containers
                .Include(x => x.Components)
                .Where(x => x.ContentId == content.Id)
                .ToListAsync(cancellationToken);

            var incomingContainers = request.Containers ?? [];
            var keepContainerIds = new HashSet<Guid>();

            var order = 1;
            foreach (var containerReq in incomingContainers.OrderBy(x => x.Ordered))
            {
                var optionsJson = SerializeValidatedOptions(containerReq.Options);
                EditorTryaContainer container;

                if (containerReq.Id.HasValue)
                {
                    container = existingContainers.FirstOrDefault(x => x.Id == containerReq.Id.Value)
                        ?? throw new ArgumentException($"Container '{containerReq.Id}' was not found.");
                    container.Update(
                        containerReq.ParentId,
                        containerReq.Component,
                        containerReq.Cols,
                        optionsJson,
                        containerReq.Publish,
                        order);
                    keepContainerIds.Add(container.Id);
                }
                else
                {
                    container = EditorTryaContainer.Create(
                        content.Id,
                        order,
                        containerReq.ParentId,
                        containerReq.Component,
                        containerReq.Cols,
                        optionsJson,
                        containerReq.Publish);
                    db.Containers.Add(container);
                    await db.SaveChangesAsync(cancellationToken);
                    keepContainerIds.Add(container.Id);
                }

                await SyncComponentsAsync(container, containerReq.Components ?? [], cancellationToken);
                order++;
            }

            var removeContainers = existingContainers.Where(x => !keepContainerIds.Contains(x.Id)).ToList();
            foreach (var container in removeContainers)
            {
                await DeleteContainerInternalAsync(container, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var tree = await BuildTreeAsync(content.Id, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(EditorContainerResponse? Response, string? Error, int StatusCode)> CreateContainerAsync(
        string component,
        Guid parentId,
        string lang,
        CreateContainerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var content = await EnsureContentAsync(component, parentId, lang, cancellationToken);
            var nextOrder = request.Ordered
                ?? (await db.Containers.Where(x => x.ContentId == content.Id).Select(x => (int?)x.Ordered).MaxAsync(cancellationToken) ?? 0) + 1;

            var container = EditorTryaContainer.Create(
                content.Id,
                nextOrder,
                request.ParentId,
                request.Component,
                request.Cols,
                SerializeValidatedOptions(request.Options),
                request.Publish);

            db.Containers.Add(container);
            content.Touch();
            await db.SaveChangesAsync(cancellationToken);

            return (ToContainerResponse(container, []), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(EditorContainerResponse? Response, string? Error, int StatusCode)> UpdateContainerAsync(
        Guid containerId,
        UpdateContainerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var container = await db.Containers
                .Include(x => x.Components)
                .FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);

            if (container is null)
            {
                return (null, "Container not found.", StatusCodes.Status404NotFound);
            }

            container.Update(
                request.ParentId,
                request.Component,
                request.Cols,
                SerializeValidatedOptions(request.Options),
                request.Publish,
                request.Ordered);

            await db.SaveChangesAsync(cancellationToken);
            var components = container.Components.OrderBy(x => x.Ordered).Select(ToComponentResponse).ToList();
            return (ToContainerResponse(container, components), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteContainerAsync(Guid containerId, CancellationToken cancellationToken)
    {
        var container = await db.Containers
            .Include(x => x.Components)
            .FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);

        if (container is null)
        {
            return ("Container not found.", StatusCodes.Status404NotFound);
        }

        await DeleteContainerInternalAsync(container, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> ReorderContainersAsync(
        ReorderRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.Id).Distinct().ToList();
        var containers = await db.Containers.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (containers.Count != ids.Count)
        {
            return ("One or more container IDs are invalid.", StatusCodes.Status400BadRequest);
        }

        var order = 1;
        foreach (var item in request.Items.OrderBy(x => x.Ordered))
        {
            containers.First(x => x.Id == item.Id).SetOrdered(order++);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    public async Task<(EditorComponentResponse? Response, string? Error, int StatusCode)> CreateComponentAsync(
        Guid containerId,
        CreateComponentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var container = await db.Containers.FirstOrDefaultAsync(x => x.Id == containerId, cancellationToken);
            if (container is null)
            {
                return (null, "Container not found.", StatusCodes.Status404NotFound);
            }

            var definition = registry.GetRequired(request.Type);
            var dataJson = PrepareComponentData(definition, request.Data);
            var optionsJson = PrepareComponentOptions(definition, request.Options);
            var nextOrder = request.Ordered
                ?? (await db.Components.Where(x => x.ContainerId == containerId).Select(x => (int?)x.Ordered).MaxAsync(cancellationToken) ?? 0) + 1;

            var component = EditorTryaComponent.Create(
                containerId,
                definition.Type,
                nextOrder,
                dataJson,
                optionsJson,
                request.Publish);

            db.Components.Add(component);
            await db.SaveChangesAsync(cancellationToken);
            return (ToComponentResponse(component), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(EditorComponentResponse? Response, string? Error, int StatusCode)> UpdateComponentAsync(
        Guid componentId,
        UpdateComponentRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var component = await db.Components.FirstOrDefaultAsync(x => x.Id == componentId, cancellationToken);
            if (component is null)
            {
                return (null, "Component not found.", StatusCodes.Status404NotFound);
            }

            var definition = registry.GetRequired(request.Type);
            component.Update(
                definition.Type,
                PrepareComponentData(definition, request.Data),
                PrepareComponentOptions(definition, request.Options),
                request.Publish,
                request.Ordered);

            await db.SaveChangesAsync(cancellationToken);
            return (ToComponentResponse(component), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteComponentAsync(Guid componentId, CancellationToken cancellationToken)
    {
        var component = await db.Components.FirstOrDefaultAsync(x => x.Id == componentId, cancellationToken);
        if (component is null)
        {
            return ("Component not found.", StatusCodes.Status404NotFound);
        }

        await DeleteComponentMediaAsync(component, cancellationToken);
        db.Components.Remove(component);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> ReorderComponentsAsync(
        ReorderRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.Id).Distinct().ToList();
        var components = await db.Components.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (components.Count != ids.Count)
        {
            return ("One or more component IDs are invalid.", StatusCodes.Status400BadRequest);
        }

        var grouped = request.Items.OrderBy(x => x.Ordered).GroupBy(x => x.ContainerId);
        foreach (var group in grouped)
        {
            var order = 1;
            foreach (var item in group)
            {
                var component = components.First(x => x.Id == item.Id);
                if (item.ContainerId.HasValue && item.ContainerId.Value != component.ContainerId)
                {
                    var exists = await db.Containers.AnyAsync(x => x.Id == item.ContainerId.Value, cancellationToken);
                    if (!exists)
                    {
                        return ($"Target container '{item.ContainerId}' was not found.", StatusCodes.Status400BadRequest);
                    }

                    component.MoveTo(item.ContainerId.Value, order++);
                }
                else
                {
                    component.SetOrdered(order++);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    public async Task<(EditorTreeResponse? Response, string? Error, int StatusCode)> CloneEditorAsync(
        string component,
        Guid parentId,
        string fromLang,
        string toLang,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var source = await EnsureContentAsync(component, parentId, fromLang, cancellationToken);
            var target = await EnsureContentAsync(component, parentId, toLang, cancellationToken);

            var targetHasContainers = await db.Containers.AnyAsync(x => x.ContentId == target.Id, cancellationToken);
            if (!targetHasContainers)
            {
                var sourceContainers = await db.Containers
                    .AsNoTracking()
                    .Where(x => x.ContentId == source.Id)
                    .OrderBy(x => x.Ordered)
                    .ToListAsync(cancellationToken);

                var sourceContainerIds = sourceContainers.Select(x => x.Id).ToList();
                var sourceComponents = await db.Components
                    .AsNoTracking()
                    .Where(x => sourceContainerIds.Contains(x.ContainerId))
                    .OrderBy(x => x.Ordered)
                    .ToListAsync(cancellationToken);

                var idMap = new Dictionary<Guid, Guid>();

                foreach (var sourceContainer in sourceContainers)
                {
                    var cloned = EditorTryaContainer.Create(
                        target.Id,
                        sourceContainer.Ordered,
                        null,
                        sourceContainer.Component,
                        sourceContainer.Cols,
                        sourceContainer.Options,
                        sourceContainer.Publish);
                    db.Containers.Add(cloned);
                    await db.SaveChangesAsync(cancellationToken);
                    idMap[sourceContainer.Id] = cloned.Id;
                }

                foreach (var sourceContainer in sourceContainers)
                {
                    if (!sourceContainer.ParentId.HasValue)
                    {
                        continue;
                    }

                    if (!idMap.TryGetValue(sourceContainer.Id, out var clonedId)
                        || !idMap.TryGetValue(sourceContainer.ParentId.Value, out var clonedParentId))
                    {
                        continue;
                    }

                    var cloned = await db.Containers.FirstAsync(x => x.Id == clonedId, cancellationToken);
                    cloned.Update(
                        clonedParentId,
                        sourceContainer.Component,
                        sourceContainer.Cols,
                        sourceContainer.Options,
                        sourceContainer.Publish,
                        sourceContainer.Ordered);
                }

                foreach (var sourceComponent in sourceComponents)
                {
                    if (!idMap.TryGetValue(sourceComponent.ContainerId, out var clonedContainerId))
                    {
                        continue;
                    }

                    var clonedComponent = EditorTryaComponent.Create(
                        clonedContainerId,
                        sourceComponent.Type,
                        sourceComponent.Ordered,
                        sourceComponent.Data,
                        sourceComponent.Options,
                        sourceComponent.Publish);
                    db.Components.Add(clonedComponent);
                }

                target.Touch();
                await db.SaveChangesAsync(cancellationToken);
            }

            await tx.CommitAsync(cancellationToken);

            var tree = await BuildTreeAsync(target.Id, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task SyncComponentsAsync(
        EditorTryaContainer container,
        IReadOnlyList<SaveEditorComponentRequest> incoming,
        CancellationToken cancellationToken)
    {
        var existing = container.Components.ToList();
        var keepIds = new HashSet<Guid>();
        var order = 1;

        foreach (var req in incoming.OrderBy(x => x.Ordered))
        {
            var definition = registry.GetRequired(req.Type);
            var dataJson = PrepareComponentData(definition, req.Data);
            var optionsJson = PrepareComponentOptions(definition, req.Options);

            if (req.Id.HasValue)
            {
                var component = existing.FirstOrDefault(x => x.Id == req.Id.Value)
                    ?? throw new ArgumentException($"Component '{req.Id}' was not found.");
                component.Update(definition.Type, dataJson, optionsJson, req.Publish, order);
                keepIds.Add(component.Id);
            }
            else
            {
                var created = EditorTryaComponent.Create(
                    container.Id,
                    definition.Type,
                    order,
                    dataJson,
                    optionsJson,
                    req.Publish);
                db.Components.Add(created);
                await db.SaveChangesAsync(cancellationToken);
                keepIds.Add(created.Id);
            }

            order++;
        }

        foreach (var component in existing.Where(x => !keepIds.Contains(x.Id)))
        {
            await DeleteComponentMediaAsync(component, cancellationToken);
            db.Components.Remove(component);
        }
    }

    private async Task DeleteContainerInternalAsync(EditorTryaContainer container, CancellationToken cancellationToken)
    {
        foreach (var component in container.Components.ToList())
        {
            await DeleteComponentMediaAsync(component, cancellationToken);
            db.Components.Remove(component);
        }

        db.Containers.Remove(container);
    }

    private async Task DeleteComponentMediaAsync(EditorTryaComponent component, CancellationToken cancellationToken)
    {
        if (!registry.TryGet(component.Type, out var definition) || definition?.FileManagerComponent is null)
        {
            return;
        }

        var files = await fileDb.FileManagers
            .Where(x => x.Component == definition.FileManagerComponent && x.ParentId == component.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var fileId in files)
        {
            await fileManagerService.DeleteAsync(fileId, cancellationToken);
        }
    }

    private async Task<EditorTryaContent> EnsureContentAsync(
        string component,
        Guid parentId,
        string languagePrefix,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(languagePrefix))
        {
            throw new ArgumentException("lang is required.", nameof(languagePrefix));
        }

        var content = EditorTryaContent.Create(component, parentId, languagePrefix);
        var existing = await db.Contents
            .FirstOrDefaultAsync(
                x => x.Component == content.Component
                     && x.ParentId == content.ParentId
                     && x.LanguagePrefix == content.LanguagePrefix,
                cancellationToken);

        if (existing is not null)
        {
            return existing;
        }

        db.Contents.Add(content);
        await db.SaveChangesAsync(cancellationToken);
        return content;
    }

    private async Task<EditorTreeResponse> BuildTreeAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await db.Contents.AsNoTracking().FirstAsync(x => x.Id == contentId, cancellationToken);
        var containers = await db.Containers
            .AsNoTracking()
            .Where(x => x.ContentId == contentId)
            .OrderBy(x => x.Ordered)
            .ToListAsync(cancellationToken);

        var containerIds = containers.Select(x => x.Id).ToList();
        var components = await db.Components
            .AsNoTracking()
            .Where(x => containerIds.Contains(x.ContainerId))
            .OrderBy(x => x.Ordered)
            .ToListAsync(cancellationToken);

        var grouped = components.GroupBy(x => x.ContainerId).ToDictionary(x => x.Key, x => x.ToList());

        return new EditorTreeResponse(
            content.Id,
            content.Component,
            content.ParentId,
            content.LanguagePrefix,
            content.Publish,
            containers.Select(c =>
            {
                var items = grouped.TryGetValue(c.Id, out var list)
                    ? list.Select(ToComponentResponse).ToList()
                    : [];
                return ToContainerResponse(c, items);
            }).ToList());
    }

    private async Task<EditorTreeResponse> BuildPublishedTreeAsync(Guid contentId, CancellationToken cancellationToken)
    {
        var content = await db.Contents.AsNoTracking().FirstAsync(x => x.Id == contentId, cancellationToken);
        var containers = await db.Containers
            .AsNoTracking()
            .Where(x => x.ContentId == contentId && x.Publish)
            .OrderBy(x => x.Ordered)
            .ToListAsync(cancellationToken);

        var containerIds = containers.Select(x => x.Id).ToList();
        var components = await db.Components
            .AsNoTracking()
            .Where(x => containerIds.Contains(x.ContainerId) && x.Publish)
            .OrderBy(x => x.Ordered)
            .ToListAsync(cancellationToken);

        var grouped = components.GroupBy(x => x.ContainerId).ToDictionary(x => x.Key, x => x.ToList());

        return new EditorTreeResponse(
            content.Id,
            content.Component,
            content.ParentId,
            content.LanguagePrefix,
            content.Publish,
            containers.Select(c =>
            {
                var items = grouped.TryGetValue(c.Id, out var list)
                    ? list.Select(ToComponentResponse).ToList()
                    : [];
                return ToContainerResponse(c, items);
            }).ToList());
    }

    private string PrepareComponentData(EditorComponentDefinition definition, JsonElement? data)
    {
        var element = data ?? ParseJsonRequired(definition.DefaultDataJson);
        var error = definition.ValidateData(element);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        if (definition.UsesHtml && element.ValueKind == JsonValueKind.Object && element.TryGetProperty("html", out var htmlProp))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(element.GetRawText(), JsonOptions)
                       ?? new Dictionary<string, JsonElement>();
            var sanitized = EditorHtmlSanitizer.Sanitize(htmlProp.GetString());
            dict["html"] = JsonSerializer.SerializeToElement(sanitized);
            return JsonSerializer.Serialize(dict, JsonOptions);
        }

        return element.GetRawText();
    }

    private string PrepareComponentOptions(EditorComponentDefinition definition, JsonElement? options)
    {
        var element = options ?? ParseJsonRequired(definition.DefaultOptionsJson);
        var error = definition.ValidateOptions(element);
        if (error is not null)
        {
            throw new ArgumentException(error);
        }

        return element.GetRawText();
    }

    private static string? SerializeValidatedOptions(JsonElement? options)
    {
        if (options is null || options.Value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return null;
        }

        if (options.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
        {
            throw new ArgumentException("options must be valid JSON object/array.");
        }

        return options.Value.GetRawText();
    }

    private static JsonElement ParseJsonRequired(string json) =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static JsonElement? ParseJsonOptional(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static EditorComponentResponse ToComponentResponse(EditorTryaComponent component) =>
        new(
            component.Id,
            component.Type,
            component.Ordered,
            component.Publish,
            ParseJsonOptional(component.Data),
            ParseJsonOptional(component.Options));

    private static EditorContainerResponse ToContainerResponse(
        EditorTryaContainer container,
        IReadOnlyList<EditorComponentResponse> components) =>
        new(
            container.Id,
            container.ParentId,
            container.Component,
            container.Cols,
            container.Ordered,
            container.Publish,
            ParseJsonOptional(container.Options),
            components);
}
