using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TishtryaCMS.Modules.FileManager.Domain;
using TishtryaCMS.Modules.FileManager.Infrastructure;
using TishtryaCMS.Modules.FileManager.Options;

namespace TishtryaCMS.Modules.FileManager.Application;

public sealed class FileManagerService(
    FileManagerDbContext db,
    IOptions<FileManagerOptions> options,
    IWebHostEnvironment environment)
{
    private readonly FileManagerOptions _options = options.Value;

    public async Task<(FileManagerResponse? Response, string? Error, int StatusCode)> UploadAsync(
        string component,
        Guid? parentId,
        IFormFile file,
        int? ordered,
        CancellationToken cancellationToken)
    {
        try
        {
            if (file is null || file.Length <= 0)
            {
                return (null, "File is required.", StatusCodes.Status400BadRequest);
            }

            if (file.Length > _options.MaxFileSizeBytes)
            {
                return (
                    null,
                    $"File exceeds maximum size of {_options.MaxFileSizeBytes} bytes.",
                    StatusCodes.Status400BadRequest);
            }

            var originalName = Path.GetFileName(file.FileName);
            var extension = Path.GetExtension(originalName).TrimStart('.').ToLowerInvariant();
            var extensionError = ValidateExtension(extension);
            if (extensionError is not null)
            {
                return (null, extensionError, StatusCodes.Status400BadRequest);
            }

            var safeComponent = NormalizeComponent(component);
            var storageRoot = ResolveStorageRoot();
            var relativeFolder = Path.Combine(safeComponent, DateTime.UtcNow.ToString("yyyy/MM")).Replace('\\', '/');
            var absoluteFolder = Path.Combine(storageRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(absoluteFolder);

            var physicalName = $"{Guid.NewGuid():N}.{extension}";
            var absolutePath = Path.Combine(absoluteFolder, physicalName);
            await using (var stream = File.Create(absolutePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            return await PersistUploadedFileAsync(
                safeComponent,
                parentId,
                ordered,
                relativeFolder,
                physicalName,
                extension,
                originalName,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public Task<(InitChunkUploadResponse? Response, string? Error, int StatusCode)> InitChunkUploadAsync(
        InitChunkUploadRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.FileSize <= 0)
            {
                return Task.FromResult<(InitChunkUploadResponse?, string?, int)>(
                    (null, "fileSize must be greater than zero.", StatusCodes.Status400BadRequest));
            }

            if (request.FileSize > _options.MaxChunkedFileSizeBytes)
            {
                return Task.FromResult<(InitChunkUploadResponse?, string?, int)>((
                    null,
                    $"File exceeds maximum chunked size of {_options.MaxChunkedFileSizeBytes} bytes.",
                    StatusCodes.Status400BadRequest));
            }

            var originalName = Path.GetFileName(request.FileName);
            var extension = Path.GetExtension(originalName).TrimStart('.').ToLowerInvariant();
            var extensionError = ValidateExtension(extension);
            if (extensionError is not null)
            {
                return Task.FromResult<(InitChunkUploadResponse?, string?, int)>(
                    (null, extensionError, StatusCodes.Status400BadRequest));
            }

            var safeComponent = NormalizeComponent(request.Component);
            var uploadId = Guid.NewGuid();
            var chunkSize = Math.Max(256 * 1024, _options.ChunkSizeBytes);
            var totalChunks = (int)Math.Ceiling(request.FileSize / (double)chunkSize);

            var session = new ChunkUploadSession
            {
                UploadId = uploadId,
                Component = safeComponent,
                ParentId = request.ParentId,
                Ordered = request.Ordered,
                OriginalFileName = originalName,
                Extension = extension,
                FileSize = request.FileSize,
                ChunkSize = chunkSize,
                TotalChunks = totalChunks,
                CreatedAtUtc = DateTime.UtcNow
            };

            var sessionDir = GetChunkSessionDir(uploadId);
            Directory.CreateDirectory(sessionDir);
            File.WriteAllText(
                Path.Combine(sessionDir, "session.json"),
                System.Text.Json.JsonSerializer.Serialize(session));

            return Task.FromResult<(InitChunkUploadResponse?, string?, int)>((
                new InitChunkUploadResponse(uploadId, chunkSize, request.FileSize, totalChunks),
                null,
                StatusCodes.Status200OK));
        }
        catch (ArgumentException ex)
        {
            return Task.FromResult<(InitChunkUploadResponse?, string?, int)>(
                (null, ex.Message, StatusCodes.Status400BadRequest));
        }
    }

    public async Task<(string? Error, int StatusCode)> UploadChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream chunkStream,
        long? contentLength,
        CancellationToken cancellationToken)
    {
        var session = LoadSession(uploadId);
        if (session is null)
        {
            return ("Upload session not found.", StatusCodes.Status404NotFound);
        }

        if (chunkIndex < 0 || chunkIndex >= session.TotalChunks)
        {
            return ("Invalid chunk index.", StatusCodes.Status400BadRequest);
        }

        if (contentLength is > 0 && contentLength > session.ChunkSize)
        {
            return ("Chunk exceeds configured chunk size.", StatusCodes.Status400BadRequest);
        }

        var sessionDir = GetChunkSessionDir(uploadId);
        var chunkPath = Path.Combine(sessionDir, $"{chunkIndex:D6}.part");
        await using (var output = File.Create(chunkPath))
        {
            await chunkStream.CopyToAsync(output, cancellationToken);
        }

        var written = new FileInfo(chunkPath).Length;
        if (written <= 0)
        {
            File.Delete(chunkPath);
            return ("Chunk is empty.", StatusCodes.Status400BadRequest);
        }

        if (chunkIndex < session.TotalChunks - 1 && written != session.ChunkSize)
        {
            File.Delete(chunkPath);
            return ("Incomplete chunk payload.", StatusCodes.Status400BadRequest);
        }

        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(FileManagerResponse? Response, string? Error, int StatusCode)> CompleteChunkUploadAsync(
        Guid uploadId,
        CancellationToken cancellationToken)
    {
        var session = LoadSession(uploadId);
        if (session is null)
        {
            return (null, "Upload session not found.", StatusCodes.Status404NotFound);
        }

        var sessionDir = GetChunkSessionDir(uploadId);
        for (var i = 0; i < session.TotalChunks; i++)
        {
            var chunkPath = Path.Combine(sessionDir, $"{i:D6}.part");
            if (!File.Exists(chunkPath))
            {
                return (null, $"Missing chunk {i}.", StatusCodes.Status400BadRequest);
            }
        }

        var storageRoot = ResolveStorageRoot();
        var relativeFolder = Path.Combine(session.Component, DateTime.UtcNow.ToString("yyyy/MM")).Replace('\\', '/');
        var absoluteFolder = Path.Combine(storageRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(absoluteFolder);

        var physicalName = $"{Guid.NewGuid():N}.{session.Extension}";
        var absolutePath = Path.Combine(absoluteFolder, physicalName);

        await using (var output = File.Create(absolutePath))
        {
            for (var i = 0; i < session.TotalChunks; i++)
            {
                var chunkPath = Path.Combine(sessionDir, $"{i:D6}.part");
                await using var input = File.OpenRead(chunkPath);
                await input.CopyToAsync(output, cancellationToken);
            }
        }

        var assembledSize = new FileInfo(absolutePath).Length;
        if (assembledSize != session.FileSize)
        {
            File.Delete(absolutePath);
            return (
                null,
                $"Assembled file size mismatch. Expected {session.FileSize}, got {assembledSize}.",
                StatusCodes.Status400BadRequest);
        }

        var result = await PersistUploadedFileAsync(
            session.Component,
            session.ParentId,
            session.Ordered,
            relativeFolder,
            physicalName,
            session.Extension,
            session.OriginalFileName,
            cancellationToken);

        TryDeleteDirectory(sessionDir);
        return result;
    }

    public async Task<IReadOnlyList<FileManagerResponse>> GetFilesAsync(
        string component,
        Guid? parentId,
        CancellationToken cancellationToken)
    {
        var safeComponent = NormalizeComponent(component);

        return await db.FileManagers
            .AsNoTracking()
            .Where(x => x.Component == safeComponent && x.ParentId == parentId && x.Publish)
            .OrderBy(x => x.Ordered)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new FileManagerResponse(
                x.Id,
                x.Component,
                x.ParentId,
                x.Ordered,
                x.Publish,
                x.Folder,
                x.Filename,
                x.Extension,
                x.FullAddress,
                x.Namefile,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetPrimaryUrlsByParentIdsAsync(
        string component,
        IEnumerable<Guid> parentIds,
        CancellationToken cancellationToken)
    {
        var ids = parentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var safeComponent = NormalizeComponent(component);
        var files = await db.FileManagers
            .AsNoTracking()
            .Where(x => x.Component == safeComponent
                        && x.ParentId.HasValue
                        && ids.Contains(x.ParentId.Value)
                        && x.Publish)
            .OrderBy(x => x.Ordered)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new { ParentId = x.ParentId!.Value, x.FullAddress })
            .ToListAsync(cancellationToken);

        return files
            .GroupBy(x => x.ParentId)
            .ToDictionary(g => g.Key, g => g.First().FullAddress);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetUrlsByIdsAsync(
        IEnumerable<Guid> ids,
        CancellationToken cancellationToken)
    {
        var fileIds = ids.Distinct().ToList();
        if (fileIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return await db.FileManagers
            .AsNoTracking()
            .Where(x => fileIds.Contains(x.Id) && x.Publish)
            .ToDictionaryAsync(x => x.Id, x => x.FullAddress, cancellationToken);
    }

    public async Task<IReadOnlyList<FileManagerResponse>> GetFilesByParentIdsAsync(
        string component,
        IEnumerable<Guid> parentIds,
        CancellationToken cancellationToken)
    {
        var ids = parentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var safeComponent = NormalizeComponent(component);
        return await db.FileManagers
            .AsNoTracking()
            .Where(x => x.Component == safeComponent
                        && x.ParentId.HasValue
                        && ids.Contains(x.ParentId.Value)
                        && x.Publish)
            .OrderBy(x => x.Ordered)
            .ThenBy(x => x.CreatedAt)
            .Select(x => new FileManagerResponse(
                x.Id,
                x.Component,
                x.ParentId,
                x.Ordered,
                x.Publish,
                x.Folder,
                x.Filename,
                x.Extension,
                x.FullAddress,
                x.Namefile,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var entry = await db.FileManagers.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entry is null)
        {
            return ("File not found.", StatusCodes.Status404NotFound);
        }

        DeletePhysicalFile(entry);
        db.FileManagers.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> UpdateOrderAsync(
        IReadOnlyList<UpdateFileOrderItem> items,
        CancellationToken cancellationToken)
    {
        if (items is null || items.Count == 0)
        {
            return ("Order items are required.", StatusCodes.Status400BadRequest);
        }

        var ids = items.Select(x => x.Id).Distinct().ToList();
        var entries = await db.FileManagers.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (entries.Count != ids.Count)
        {
            return ("One or more file IDs are invalid.", StatusCodes.Status400BadRequest);
        }

        var map = items.ToDictionary(x => x.Id, x => x.Ordered);
        foreach (var entry in entries)
        {
            entry.SetOrdered(map[entry.Id]);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    private async Task<(FileManagerResponse? Response, string? Error, int StatusCode)> PersistUploadedFileAsync(
        string safeComponent,
        Guid? parentId,
        int? ordered,
        string relativeFolder,
        string physicalName,
        string extension,
        string originalName,
        CancellationToken cancellationToken)
    {
        var order = ordered ?? await GetNextOrderAsync(safeComponent, parentId, cancellationToken);
        var publicPrefix = _options.PublicPathPrefix.TrimEnd('/');
        var fullAddress = $"{publicPrefix}/{relativeFolder}/{physicalName}".Replace("//", "/");

        if (!IsGalleryComponent(safeComponent) && parentId.HasValue)
        {
            var existing = await db.FileManagers
                .Where(x => x.Component == safeComponent && x.ParentId == parentId)
                .ToListAsync(cancellationToken);

            foreach (var item in existing)
            {
                DeletePhysicalFile(item);
                db.FileManagers.Remove(item);
            }
        }

        var entry = FileManagerEntry.Create(
            safeComponent,
            parentId,
            order,
            relativeFolder,
            physicalName,
            extension,
            fullAddress,
            originalName);

        db.FileManagers.Add(entry);
        await db.SaveChangesAsync(cancellationToken);
        return (ToResponse(entry), null, StatusCodes.Status201Created);
    }

    private async Task<int> GetNextOrderAsync(string component, Guid? parentId, CancellationToken cancellationToken)
    {
        var max = await db.FileManagers
            .Where(x => x.Component == component && x.ParentId == parentId)
            .Select(x => (int?)x.Ordered)
            .MaxAsync(cancellationToken);

        return (max ?? 0) + 1;
    }

    private string ResolveStorageRoot()
    {
        if (Path.IsPathRooted(_options.StorageRoot))
        {
            return _options.StorageRoot;
        }

        var contentRoot = environment.ContentRootPath;
        return Path.GetFullPath(Path.Combine(contentRoot, _options.StorageRoot));
    }

    private string GetChunkSessionDir(Guid uploadId)
    {
        return Path.Combine(ResolveStorageRoot(), "_chunks", uploadId.ToString("N"));
    }

    private ChunkUploadSession? LoadSession(Guid uploadId)
    {
        var path = Path.Combine(GetChunkSessionDir(uploadId), "session.json");
        if (!File.Exists(path))
        {
            return null;
        }

        return System.Text.Json.JsonSerializer.Deserialize<ChunkUploadSession>(File.ReadAllText(path));
    }

    private string? ValidateExtension(string extension)
    {
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
        {
            return $"File type '{extension}' is not allowed. Allowed: {string.Join(", ", _options.AllowedExtensions)}";
        }

        return null;
    }

    private void DeletePhysicalFile(FileManagerEntry entry)
    {
        try
        {
            var storageRoot = ResolveStorageRoot();
            var relative = string.IsNullOrWhiteSpace(entry.Folder)
                ? entry.Filename
                : Path.Combine(entry.Folder.Replace('/', Path.DirectorySeparatorChar), entry.Filename);
            var absolutePath = Path.Combine(storageRoot, relative);
            if (File.Exists(absolutePath))
            {
                File.Delete(absolutePath);
            }
        }
        catch
        {
            // Best-effort physical cleanup; DB delete still proceeds.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup.
        }
    }

    private static bool IsGalleryComponent(string component) =>
        component.Contains("gallery", StringComparison.OrdinalIgnoreCase)
        && !component.Contains("thumbnail", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("component is required.", nameof(component));
        }

        return component.Trim().ToLowerInvariant();
    }

    private static FileManagerResponse ToResponse(FileManagerEntry entry) =>
        new(
            entry.Id,
            entry.Component,
            entry.ParentId,
            entry.Ordered,
            entry.Publish,
            entry.Folder,
            entry.Filename,
            entry.Extension,
            entry.FullAddress,
            entry.Namefile,
            entry.CreatedAt,
            entry.UpdatedAt);

    private sealed class ChunkUploadSession
    {
        public Guid UploadId { get; set; }
        public string Component { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
        public int? Ordered { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public int ChunkSize { get; set; }
        public int TotalChunks { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }
}
