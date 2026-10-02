using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Qa.Domain;

public sealed class QaAnswer : Entity
{
    public Guid QuestionId { get; private set; }
    public int Ordered { get; private set; } = 1;
    public bool Publish { get; private set; } = true;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public QaQuestion? Question { get; private set; }
    public ICollection<QaAnswerTranslation> Translations { get; private set; } = new List<QaAnswerTranslation>();

    private QaAnswer()
    {
    }

    public static QaAnswer Create(Guid questionId, int ordered, bool publish = true)
    {
        if (questionId == Guid.Empty)
        {
            throw new ArgumentException("questionId is required.", nameof(questionId));
        }

        return new QaAnswer
        {
            Id = Guid.Empty,
            QuestionId = questionId,
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
}
