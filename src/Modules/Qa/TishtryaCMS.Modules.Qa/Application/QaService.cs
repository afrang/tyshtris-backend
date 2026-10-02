using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Qa.Domain;
using TishtryaCMS.Modules.Qa.Infrastructure;

namespace TishtryaCMS.Modules.Qa.Application;

public sealed class QaService(QaDbContext db)
{
    public async Task<(QaTreeResponse? Response, string? Error, int StatusCode)> GetAsync(
        string component,
        Guid parentId,
        string lang,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedComponent = QaQuestion.NormalizeComponent(component);
            var langPrefix = LanguagePrefix.Normalize(lang);

            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var tree = await BuildTreeAsync(normalizedComponent, parentId, langPrefix, publishedOnly, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(QaTreeResponse? Response, string? Error, int StatusCode)> SaveAsync(
        string component,
        Guid parentId,
        string lang,
        SaveQaTreeRequest request,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var normalizedComponent = QaQuestion.NormalizeComponent(component);
            var langPrefix = LanguagePrefix.Normalize(lang);

            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var existing = await db.Questions
                .Include(x => x.Translations)
                .Include(x => x.Answers)
                    .ThenInclude(a => a.Translations)
                .Where(x => x.Component == normalizedComponent && x.ParentId == parentId)
                .ToListAsync(cancellationToken);

            var keepQuestionIds = new HashSet<Guid>();
            var incoming = request.Questions ?? [];
            var questionOrder = 1;

            foreach (var questionReq in incoming.OrderBy(x => x.Ordered))
            {
                QaQuestion question;
                if (questionReq.Id.HasValue)
                {
                    question = existing.FirstOrDefault(x => x.Id == questionReq.Id.Value)
                        ?? throw new ArgumentException($"Question '{questionReq.Id}' was not found.");
                    question.Update(questionOrder, questionReq.Publish);
                    UpsertQuestionTranslation(question, langPrefix, questionReq.QuestionText);
                    keepQuestionIds.Add(question.Id);
                }
                else
                {
                    question = QaQuestion.Create(normalizedComponent, parentId, questionOrder, questionReq.Publish);
                    db.Questions.Add(question);
                    await db.SaveChangesAsync(cancellationToken);
                    UpsertQuestionTranslation(question, langPrefix, questionReq.QuestionText);
                    keepQuestionIds.Add(question.Id);
                }

                await SyncAnswersAsync(question, questionReq.Answers ?? [], langPrefix, cancellationToken);
                questionOrder++;
            }

            var removeQuestions = existing.Where(x => !keepQuestionIds.Contains(x.Id)).ToList();
            if (removeQuestions.Count > 0)
            {
                db.Questions.RemoveRange(removeQuestions);
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var tree = await BuildTreeAsync(normalizedComponent, parentId, langPrefix, publishedOnly: false, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<(QaQuestionResponse? Response, string? Error, int StatusCode)> CreateQuestionAsync(
        string component,
        Guid parentId,
        string lang,
        CreateQaQuestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var normalizedComponent = QaQuestion.NormalizeComponent(component);
            var langPrefix = LanguagePrefix.Normalize(lang);

            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var nextOrder = request.Ordered
                ?? (await db.Questions
                    .Where(x => x.Component == normalizedComponent && x.ParentId == parentId)
                    .Select(x => (int?)x.Ordered)
                    .MaxAsync(cancellationToken) ?? 0) + 1;

            var question = QaQuestion.Create(normalizedComponent, parentId, nextOrder, request.Publish);
            db.Questions.Add(question);
            await db.SaveChangesAsync(cancellationToken);

            UpsertQuestionTranslation(question, langPrefix, request.QuestionText);
            await db.SaveChangesAsync(cancellationToken);

            return (ToQuestionResponse(question, langPrefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(QaQuestionResponse? Response, string? Error, int StatusCode)> UpdateQuestionAsync(
        Guid questionId,
        string lang,
        UpdateQaQuestionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var langPrefix = LanguagePrefix.Normalize(lang);
            var question = await db.Questions
                .Include(x => x.Translations)
                .Include(x => x.Answers)
                    .ThenInclude(a => a.Translations)
                .FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);

            if (question is null)
            {
                return (null, "Question not found.", StatusCodes.Status404NotFound);
            }

            question.Update(request.Ordered, request.Publish);
            UpsertQuestionTranslation(question, langPrefix, request.QuestionText);
            await db.SaveChangesAsync(cancellationToken);

            return (ToQuestionResponse(question, langPrefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteQuestionAsync(
        Guid questionId,
        CancellationToken cancellationToken)
    {
        var question = await db.Questions.FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);
        if (question is null)
        {
            return ("Question not found.", StatusCodes.Status404NotFound);
        }

        db.Questions.Remove(question);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(QaAnswerResponse? Response, string? Error, int StatusCode)> CreateAnswerAsync(
        Guid questionId,
        string lang,
        CreateQaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var langPrefix = LanguagePrefix.Normalize(lang);
            var question = await db.Questions
                .Include(x => x.Answers)
                .FirstOrDefaultAsync(x => x.Id == questionId, cancellationToken);

            if (question is null)
            {
                return (null, "Question not found.", StatusCodes.Status404NotFound);
            }

            var nextOrder = request.Ordered
                ?? (question.Answers.Select(x => (int?)x.Ordered).Max() ?? 0) + 1;

            var answer = QaAnswer.Create(question.Id, nextOrder, request.Publish);
            db.Answers.Add(answer);
            question.Touch();
            await db.SaveChangesAsync(cancellationToken);

            UpsertAnswerTranslation(answer, langPrefix, request.AnswerText);
            await db.SaveChangesAsync(cancellationToken);

            return (ToAnswerResponse(answer, langPrefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(QaAnswerResponse? Response, string? Error, int StatusCode)> UpdateAnswerAsync(
        Guid answerId,
        string lang,
        UpdateQaAnswerRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var langPrefix = LanguagePrefix.Normalize(lang);
            var answer = await db.Answers
                .Include(x => x.Translations)
                .FirstOrDefaultAsync(x => x.Id == answerId, cancellationToken);

            if (answer is null)
            {
                return (null, "Answer not found.", StatusCodes.Status404NotFound);
            }

            answer.Update(request.Ordered, request.Publish);
            UpsertAnswerTranslation(answer, langPrefix, request.AnswerText);
            await db.SaveChangesAsync(cancellationToken);

            return (ToAnswerResponse(answer, langPrefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAnswerAsync(
        Guid answerId,
        CancellationToken cancellationToken)
    {
        var answer = await db.Answers.FirstOrDefaultAsync(x => x.Id == answerId, cancellationToken);
        if (answer is null)
        {
            return ("Answer not found.", StatusCodes.Status404NotFound);
        }

        db.Answers.Remove(answer);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(string? Error, int StatusCode)> ReorderQuestionsAsync(
        ReorderRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.Id).ToHashSet();
        var questions = await db.Questions.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (questions.Count != ids.Count)
        {
            return ("One or more questions were not found.", StatusCodes.Status404NotFound);
        }

        foreach (var item in request.Items)
        {
            questions.First(x => x.Id == item.Id).SetOrdered(item.Ordered);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    public async Task<(string? Error, int StatusCode)> ReorderAnswersAsync(
        ReorderRequest request,
        CancellationToken cancellationToken)
    {
        var ids = request.Items.Select(x => x.Id).ToHashSet();
        var answers = await db.Answers.Where(x => ids.Contains(x.Id)).ToListAsync(cancellationToken);
        if (answers.Count != ids.Count)
        {
            return ("One or more answers were not found.", StatusCodes.Status404NotFound);
        }

        foreach (var item in request.Items)
        {
            answers.First(x => x.Id == item.Id).SetOrdered(item.Ordered);
        }

        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status200OK);
    }

    public async Task<(QaTreeResponse? Response, string? Error, int StatusCode)> CloneAsync(
        string component,
        Guid parentId,
        string fromLang,
        string toLang,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var normalizedComponent = QaQuestion.NormalizeComponent(component);
            var from = LanguagePrefix.Normalize(fromLang);
            var to = LanguagePrefix.Normalize(toLang);

            if (from == to)
            {
                return (null, "from and to languages must be different.", StatusCodes.Status400BadRequest);
            }

            if (parentId == Guid.Empty)
            {
                return (null, "parentId is required.", StatusCodes.Status400BadRequest);
            }

            var questions = await db.Questions
                .Include(x => x.Translations)
                .Include(x => x.Answers)
                    .ThenInclude(a => a.Translations)
                .Where(x => x.Component == normalizedComponent && x.ParentId == parentId)
                .ToListAsync(cancellationToken);

            foreach (var question in questions)
            {
                var sourceQuestion = question.Translations.FirstOrDefault(t => t.LanguagePrefix == from);
                if (sourceQuestion is null)
                {
                    continue;
                }

                UpsertQuestionTranslation(question, to, sourceQuestion.QuestionText);

                foreach (var answer in question.Answers)
                {
                    var sourceAnswer = answer.Translations.FirstOrDefault(t => t.LanguagePrefix == from);
                    if (sourceAnswer is null)
                    {
                        continue;
                    }

                    UpsertAnswerTranslation(answer, to, sourceAnswer.AnswerText);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            var tree = await BuildTreeAsync(normalizedComponent, parentId, to, publishedOnly: false, cancellationToken);
            return (tree, null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            await tx.RollbackAsync(cancellationToken);
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
        catch (Exception)
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task SyncAnswersAsync(
        QaQuestion question,
        IReadOnlyList<SaveQaAnswerRequest> incoming,
        string langPrefix,
        CancellationToken cancellationToken)
    {
        var existing = question.Answers.ToList();
        var keepIds = new HashSet<Guid>();
        var order = 1;

        foreach (var answerReq in incoming.OrderBy(x => x.Ordered))
        {
            QaAnswer answer;
            if (answerReq.Id.HasValue)
            {
                answer = existing.FirstOrDefault(x => x.Id == answerReq.Id.Value)
                    ?? throw new ArgumentException($"Answer '{answerReq.Id}' was not found.");
                answer.Update(order, answerReq.Publish);
                UpsertAnswerTranslation(answer, langPrefix, answerReq.AnswerText);
                keepIds.Add(answer.Id);
            }
            else
            {
                answer = QaAnswer.Create(question.Id, order, answerReq.Publish);
                db.Answers.Add(answer);
                await db.SaveChangesAsync(cancellationToken);
                UpsertAnswerTranslation(answer, langPrefix, answerReq.AnswerText);
                keepIds.Add(answer.Id);
            }

            order++;
        }

        var remove = existing.Where(x => !keepIds.Contains(x.Id)).ToList();
        if (remove.Count > 0)
        {
            db.Answers.RemoveRange(remove);
        }
    }

    private void UpsertQuestionTranslation(QaQuestion question, string langPrefix, string questionText)
    {
        var translation = question.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);
        if (translation is null)
        {
            translation = QaQuestionTranslation.Create(question.Id, langPrefix, questionText);
            question.Translations.Add(translation);
            db.QuestionTranslations.Add(translation);
        }
        else
        {
            translation.Update(questionText);
        }

        question.Touch();
    }

    private void UpsertAnswerTranslation(QaAnswer answer, string langPrefix, string answerText)
    {
        var translation = answer.Translations.FirstOrDefault(x => x.LanguagePrefix == langPrefix);
        if (translation is null)
        {
            translation = QaAnswerTranslation.Create(answer.Id, langPrefix, answerText);
            answer.Translations.Add(translation);
            db.AnswerTranslations.Add(translation);
        }
        else
        {
            translation.Update(answerText);
        }

        answer.Touch();
    }

    private async Task<QaTreeResponse> BuildTreeAsync(
        string component,
        Guid parentId,
        string langPrefix,
        bool publishedOnly,
        CancellationToken cancellationToken)
    {
        var query = db.Questions
            .AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Answers)
                .ThenInclude(a => a.Translations)
            .Where(x => x.Component == component && x.ParentId == parentId);

        if (publishedOnly)
        {
            query = query.Where(x => x.Publish);
        }

        var questions = await query
            .OrderBy(x => x.Ordered)
            .ToListAsync(cancellationToken);

        var mapped = questions
            .Select(q => ToQuestionResponse(q, langPrefix, publishedOnly))
            .ToList();

        return new QaTreeResponse(component, parentId, langPrefix, mapped);
    }

    private static QaQuestionResponse ToQuestionResponse(
        QaQuestion question,
        string langPrefix,
        bool publishedOnly = false)
    {
        var text = question.Translations.FirstOrDefault(t => t.LanguagePrefix == langPrefix)?.QuestionText
            ?? question.Translations.Select(t => t.QuestionText).FirstOrDefault()
            ?? string.Empty;

        var answers = question.Answers
            .Where(a => !publishedOnly || a.Publish)
            .OrderBy(a => a.Ordered)
            .Select(a => ToAnswerResponse(a, langPrefix))
            .ToList();

        return new QaQuestionResponse(question.Id, question.Ordered, question.Publish, text, answers);
    }

    private static QaAnswerResponse ToAnswerResponse(QaAnswer answer, string langPrefix)
    {
        var text = answer.Translations.FirstOrDefault(t => t.LanguagePrefix == langPrefix)?.AnswerText
            ?? answer.Translations.Select(t => t.AnswerText).FirstOrDefault()
            ?? string.Empty;

        return new QaAnswerResponse(answer.Id, answer.Ordered, answer.Publish, text);
    }
}
