namespace TishtryaCMS.Modules.Forms.Application;

public sealed record FormListItemResponse(
    Guid Id,
    string Title,
    string Slug,
    bool IsPublished,
    int FieldCount,
    int SubmissionCount,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LanguagePrefix);

public sealed record FormFieldResponse(
    Guid Id,
    string FieldKey,
    string FieldType,
    string Label,
    string? Placeholder,
    string? HelpText,
    IReadOnlyList<string> Options,
    bool IsRequired,
    int SortOrder);

public sealed record FormResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string SubmitButtonText,
    string? SuccessMessage,
    bool IsPublished,
    Guid? CreatedBy,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? LanguagePrefix,
    IReadOnlyList<FormFieldResponse> Fields);

public sealed record UpsertFormRequest(
    string Title,
    string Slug,
    string? Description,
    string? SubmitButtonText,
    string? SuccessMessage,
    bool IsPublished);

public sealed record UpsertFormFieldRequest(
    Guid? Id,
    string FieldKey,
    string FieldType,
    string Label,
    string? Placeholder,
    string? HelpText,
    IReadOnlyList<string>? Options,
    bool IsRequired,
    int SortOrder);

public sealed record SyncFormFieldsRequest(IReadOnlyList<UpsertFormFieldRequest> Fields);

public sealed record FormSubmissionResponse(
    Guid Id,
    Guid FormId,
    string PayloadJson,
    string? LanguagePrefix,
    string? IpAddress,
    DateTime CreatedAt);

public sealed record SubmitFormRequest(IReadOnlyDictionary<string, string?> Values);

public sealed record PublicFormResponse(
    Guid Id,
    string Title,
    string Slug,
    string? Description,
    string SubmitButtonText,
    string? SuccessMessage,
    IReadOnlyList<FormFieldResponse> Fields);
