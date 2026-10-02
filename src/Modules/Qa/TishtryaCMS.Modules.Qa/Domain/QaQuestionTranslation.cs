using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Qa.Domain;

public sealed class QaQuestionTranslation : Entity
{
    public Guid QuestionId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string QuestionText { get; private set; } = string.Empty;

    public QaQuestion? Question { get; private set; }

    private QaQuestionTranslation()
    {
    }

    public static QaQuestionTranslation Create(Guid questionId, string languagePrefix, string questionText)
    {
        return new QaQuestionTranslation
        {
            Id = Guid.Empty,
            QuestionId = questionId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            QuestionText = NormalizeRequired(questionText, nameof(questionText))
        };
    }

    public void Update(string questionText)
    {
        QuestionText = NormalizeRequired(questionText, nameof(questionText));
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("languagePrefix is required.", nameof(prefix));
        }

        return prefix.Trim().ToLowerInvariant().Replace('_', '-');
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
