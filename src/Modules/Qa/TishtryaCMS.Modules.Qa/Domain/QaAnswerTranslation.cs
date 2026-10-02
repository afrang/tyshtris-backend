using TishtryaCMS.SharedKernel;

namespace TishtryaCMS.Modules.Qa.Domain;

public sealed class QaAnswerTranslation : Entity
{
    public Guid AnswerId { get; private set; }
    public string LanguagePrefix { get; private set; } = string.Empty;
    public string AnswerText { get; private set; } = string.Empty;

    public QaAnswer? Answer { get; private set; }

    private QaAnswerTranslation()
    {
    }

    public static QaAnswerTranslation Create(Guid answerId, string languagePrefix, string answerText)
    {
        return new QaAnswerTranslation
        {
            Id = Guid.Empty,
            AnswerId = answerId,
            LanguagePrefix = NormalizePrefix(languagePrefix),
            AnswerText = NormalizeRequired(answerText, nameof(answerText))
        };
    }

    public void Update(string answerText)
    {
        AnswerText = NormalizeRequired(answerText, nameof(answerText));
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
