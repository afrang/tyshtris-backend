using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.EditorTrya.Domain;

public sealed class EditorTryaContainer : Entity
{
    public Guid ContentId { get; private set; }
    public Guid? ParentId { get; private set; }
    public string? Component { get; private set; }
    public int? Cols { get; private set; }
    public int Ordered { get; private set; } = 1;
    public bool Publish { get; private set; } = true;
    public string? Options { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public EditorTryaContent? Content { get; private set; }
    public ICollection<EditorTryaComponent> Components { get; private set; } = new List<EditorTryaComponent>();

    private EditorTryaContainer()
    {
    }

    public static EditorTryaContainer Create(
        Guid contentId,
        int ordered,
        Guid? parentId,
        string? component,
        int? cols,
        string? options,
        bool publish = true)
    {
        if (contentId == Guid.Empty)
        {
            throw new ArgumentException("contentId is required.", nameof(contentId));
        }

        return new EditorTryaContainer
        {
            Id = Guid.Empty,
            ContentId = contentId,
            ParentId = parentId,
            Component = string.IsNullOrWhiteSpace(component) ? null : component.Trim().ToLowerInvariant(),
            Cols = cols,
            Ordered = ordered < 1 ? 1 : ordered,
            Publish = publish,
            Options = options,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(Guid? parentId, string? component, int? cols, string? options, bool publish, int ordered)
    {
        ParentId = parentId;
        Component = string.IsNullOrWhiteSpace(component) ? null : component.Trim().ToLowerInvariant();
        Cols = cols;
        Options = options;
        Publish = publish;
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOrdered(int ordered)
    {
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }
}
