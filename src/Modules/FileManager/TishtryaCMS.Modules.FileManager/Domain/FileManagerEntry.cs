using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.FileManager.Domain;

public sealed class FileManagerEntry : Entity
{
    public string Component { get; private set; } = string.Empty;
    public Guid? ParentId { get; private set; }
    public int Ordered { get; private set; } = 1;
    public bool Publish { get; private set; } = true;
    public string? Folder { get; private set; }
    public string Filename { get; private set; } = string.Empty;
    public string Extension { get; private set; } = string.Empty;
    public string FullAddress { get; private set; } = string.Empty;
    public string? Namefile { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private FileManagerEntry()
    {
    }

    public static FileManagerEntry Create(
        string component,
        Guid? parentId,
        int ordered,
        string? folder,
        string filename,
        string extension,
        string fullAddress,
        string? namefile)
    {
        return new FileManagerEntry
        {
            Id = Guid.Empty,
            Component = NormalizeComponent(component),
            ParentId = parentId,
            Ordered = ordered < 1 ? 1 : ordered,
            Publish = true,
            Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim(),
            Filename = NormalizeRequired(filename, nameof(filename)),
            Extension = NormalizeExtension(extension),
            FullAddress = NormalizeRequired(fullAddress, nameof(fullAddress)),
            Namefile = string.IsNullOrWhiteSpace(namefile) ? null : namefile.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetOrdered(int ordered)
    {
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPublish(bool publish)
    {
        Publish = publish;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ReplaceFile(
        string? folder,
        string filename,
        string extension,
        string fullAddress,
        string? namefile)
    {
        Folder = string.IsNullOrWhiteSpace(folder) ? null : folder.Trim();
        Filename = NormalizeRequired(filename, nameof(filename));
        Extension = NormalizeExtension(extension);
        FullAddress = NormalizeRequired(fullAddress, nameof(fullAddress));
        Namefile = string.IsNullOrWhiteSpace(namefile) ? null : namefile.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeComponent(string component)
    {
        var value = NormalizeRequired(component, nameof(component)).ToLowerInvariant();
        return value;
    }

    private static string NormalizeExtension(string extension)
    {
        var value = NormalizeRequired(extension, nameof(extension)).TrimStart('.').ToLowerInvariant();
        return value;
    }

    private static string NormalizeRequired(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} is required.", fieldName);
        }

        return value.Trim();
    }
}
