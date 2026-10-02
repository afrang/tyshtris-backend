using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Qa.Domain;

public sealed class QaQuestion : Entity
{
    public string Component { get; private set; } = string.Empty;
    public Guid ParentId { get; private set; }
    public int Ordered { get; private set; } = 1;
    public bool Publish { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public ICollection<QaQuestionTranslation> Translations { get; private set; } = new List<QaQuestionTranslation>();
    public ICollection<QaAnswer> Answers { get; private set; } = new List<QaAnswer>();

    private QaQuestion()
    {
    }

    public static QaQuestion Create(string component, Guid parentId, int ordered, bool publish = true)
    {
        if (parentId == Guid.Empty)
        {
            throw new ArgumentException("parentId is required.", nameof(parentId));
        }

        return new QaQuestion
        {
            Id = Guid.Empty,
            Component = NormalizeComponent(component),
            ParentId = parentId,
            Ordered = ordered < 1 ? 1 : ordered,
            Publish = publish,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(int ordered, bool publish)
    {
        Ordered = ordered < 1 ? 1 : ordered;
        Publish = publish;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetOrdered(int ordered)
    {
        Ordered = ordered < 1 ? 1 : ordered;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    internal static string NormalizeComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ArgumentException("component is required.", nameof(component));
        }

        return component.Trim().ToLowerInvariant();
    }
}
