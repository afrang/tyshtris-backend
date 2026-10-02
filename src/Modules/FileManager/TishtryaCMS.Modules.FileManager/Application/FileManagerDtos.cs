namespace TishtryaCMS.Modules.FileManager.Application;

public sealed record FileManagerResponse(
    Guid Id,
    string Component,
    Guid? ParentId,
    int Ordered,
    bool Publish,
    string? Folder,
    string Filename,
    string Extension,
    string FullAddress,
    string? Namefile,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record UpdateFileOrderItem(Guid Id, int Ordered);

public sealed record InitChunkUploadRequest(
    string Component,
    Guid? ParentId,
    string FileName,
    long FileSize,
    int? Ordered);

public sealed record InitChunkUploadResponse(
    Guid UploadId,
    int ChunkSize,
    long FileSize,
    int TotalChunks);

public sealed record CompleteChunkUploadRequest(Guid UploadId);
