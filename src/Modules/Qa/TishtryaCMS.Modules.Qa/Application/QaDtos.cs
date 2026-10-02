namespace TishtryaCMS.Modules.Qa.Application;

public sealed record QaAnswerResponse(
    Guid Id,
    int Ordered,
    bool Publish,
    string AnswerText);

public sealed record QaQuestionResponse(
    Guid Id,
    int Ordered,
    bool Publish,
    string QuestionText,
    IReadOnlyList<QaAnswerResponse> Answers);

public sealed record QaTreeResponse(
    string Component,
    Guid ParentId,
    string LanguagePrefix,
    IReadOnlyList<QaQuestionResponse> Questions);

public sealed record SaveQaAnswerRequest(
    Guid? Id,
    int Ordered,
    bool Publish,
    string AnswerText);

public sealed record SaveQaQuestionRequest(
    Guid? Id,
    int Ordered,
    bool Publish,
    string QuestionText,
    IReadOnlyList<SaveQaAnswerRequest>? Answers);

public sealed record SaveQaTreeRequest(
    IReadOnlyList<SaveQaQuestionRequest> Questions);

public sealed record CreateQaQuestionRequest(
    string QuestionText,
    int? Ordered,
    bool Publish);

public sealed record UpdateQaQuestionRequest(
    string QuestionText,
    int Ordered,
    bool Publish);

public sealed record CreateQaAnswerRequest(
    string AnswerText,
    int? Ordered,
    bool Publish);

public sealed record UpdateQaAnswerRequest(
    string AnswerText,
    int Ordered,
    bool Publish);

public sealed record ReorderItemRequest(Guid Id, int Ordered);

public sealed record ReorderRequest(IReadOnlyList<ReorderItemRequest> Items);
