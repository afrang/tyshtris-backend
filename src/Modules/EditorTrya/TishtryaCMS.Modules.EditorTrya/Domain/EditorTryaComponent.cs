using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.EditorTrya.Domain;

public sealed class EditorTryaComponent : Entity
{
    public Guid ContainerId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public int Ordered { get; private set; } = 1;
    public bool Publish { get; private set; } = true;
    public string? Data { get; private set; }
    public string? Options { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public EditorTryaContainer? Container { get; private set; }

    private EditorTryaComponent()
    {
    }

    public static EditorTryaComponent Create(
        Guid containerId,
        string type,
        int ordered,
        string? data,
        string? options,
        bool publish = true)
    {
        if (containerId == Guid.Empty)
        {
            throw new ArgumentException("containerId is required.", nameof(containerId));
        }

        return new EditorTryaComponent
        {
            Id = Guid.Empty,
            ContainerId = containerId,
            Type = NormalizeType(type),
            Ordered = ordered < 1 ? 1 : ordered,
            Publish = publish,
            Data = data,
            Options = options,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string type, string? data, string? options, bool publish, int ordered)
    {
        Type = NormalizeType(type);
        Data = data;
        Options = options;
        Publish = publish;
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveTo(Guid containerId, int ordered)
    {
        if (containerId == Guid.Empty)
        {
            throw new ArgumentException("containerId is required.", nameof(containerId));
        }

        ContainerId = containerId;
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOrdered(int ordered)
    {
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string NormalizeType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("type is required.", nameof(type));
        }

        return type.Trim().ToLowerInvariant();
    }
}
