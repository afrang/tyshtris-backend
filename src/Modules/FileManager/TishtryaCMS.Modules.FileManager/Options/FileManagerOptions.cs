namespace TishtryaCMS.Modules.FileManager.Options;

public sealed class FileManagerOptions
{
    public const string SectionName = "FileManager";

    /// <summary>Absolute or content-root-relative storage path.</summary>
    public string StorageRoot { get; set; } = "wwwroot/uploads";

    /// <summary>Public URL prefix for served files.</summary>
    public string PublicPathPrefix { get; set; } = "/uploads";

    /// <summary>Maximum single-request upload size in bytes (default 5 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Maximum chunked upload size in bytes (default 500 MB).</summary>
    public long MaxChunkedFileSizeBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Preferred chunk size for resumable uploads (default 2 MB).</summary>
    public int ChunkSizeBytes { get; set; } = 2 * 1024 * 1024;

    public string[] AllowedExtensions { get; set; } =
    [
        "jpg", "jpeg", "png", "webp", "gif",
        "mp4", "webm", "ogg", "mov", "m4v",
        "mp3", "wav", "m4a", "aac", "oga"
    ];
}
