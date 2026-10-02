using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.EditorTrya.Domain;

public sealed class EditorTryaContent : Entity
{
    public string Component { get; private set; } = string.Empty;
    public Guid ParentId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public bool Publish { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<EditorTryaContainer> Containers { get; private set; } = new List<EditorTryaContainer>();

    private EditorTryaContent()
    {
    }

    public static EditorTryaContent Create(string component, Guid parentId, string languagePrefix)
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("parentId is required.", nameof(parentId));
        }

        return new EditorTryaContent
        {
            Id = Guid.Empty,
            Component = NormalizeComponent(component),
            ParentId = parentId,
            LanguagePrefix = NormalizeLanguagePrefix(languagePrefix),
            Publish = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPublish(bool publish)
    {
        Publish = publish;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("component is required.", nameof(component));
        }

        return component.Trim().ToLowerInvariant();
    }

    private static string NormalizeLanguagePrefix(string languagePrefix)
    {
        if (string.IsNullOrWhiteSpace(languagePrefix))
        {
            throw new ArgumentException("languagePrefix is required.", nameof(languagePrefix));
        }

        return languagePrefix.Trim().ToLowerInvariant().Replace('_', '-');
    }
}
