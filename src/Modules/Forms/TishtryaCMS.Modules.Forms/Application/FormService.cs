using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Forms.Domain;
using TishtryaCMS.Modules.Forms.Infrastructure;

namespace TishtryaCMS.Modules.Forms.Application;

public sealed class FormService(FormsDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<FormListItemResponse>> GetAllAsync(
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var query = db.Forms.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Fields)
            .AsQueryable();

        var userId = TryGetUserId(user);
        if (userId.HasValue && !IsAdmin(user))
        {
            query = query.Where(x => x.CreatedBy == userId.Value);
        }

        var forms = await query
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync(cancellationToken);

        var formIds = forms.Select(x => x.Id).ToList();
        var submissionCounts = formIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.FormSubmissions.AsNoTracking()
                .Where(x => formIds.Contains(x.FormId))
                .GroupBy(x => x.FormId)
                .Select(g => new { FormId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.FormId, x => x.Count, cancellationToken);

        return forms.Select(form =>
        {
            var (title, slug, languagePrefix) = ResolveListFields(form, prefix);
            submissionCounts.TryGetValue(form.Id, out var submissions);
            return new FormListItemResponse(
                form.Id,
                title,
                slug,
                form.IsPublished,
                form.Fields.Count,
                submissions,
                form.CreatedBy,
                form.CreatedAt,
                form.UpdatedAt,
                languagePrefix);
        }).ToList();
    }

    public async Task<(FormResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        var form = await db.Forms.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Fields)
                .ThenInclude(f => f.Translations)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (form is null)
        {
            return (null, "Form not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(form, user);
        if (accessError is not null)
        {
            return (null, accessError, StatusCodes.Status403Forbidden);
        }

        return (ToResponse(form, LanguagePrefix.NormalizeOptional(lang)), null, StatusCodes.Status200OK);
    }

    public async Task<(FormResponse? Response, string? Error, int StatusCode)> CreateAsync(
        UpsertFormRequest request,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            if (await SlugExistsAsync(request.Slug, prefix, excludeFormId: null, cancellationToken))
            {
                return (null, "A form with this slug already exists.", StatusCodes.Status409Conflict);
            }

            var form = FormDefinition.Create(
                request.Title,
                request.Slug,
                request.Description,
                request.SubmitButtonText,
                request.SuccessMessage,
                request.IsPublished,
                TryGetUserId(user));

            db.Forms.Add(form);
            await db.SaveChangesAsync(cancellationToken);

            db.FormTranslations.Add(FormTranslation.Create(
                form.Id,
                prefix,
                request.Title,
                request.Slug,
                request.Description,
                request.SubmitButtonText,
                request.SuccessMessage));

            await db.SaveChangesAsync(cancellationToken);

            var created = await db.Forms.AsNoTracking()
                .Include(x => x.Translations)
                .Include(x => x.Fields)
                    .ThenInclude(f => f.Translations)
                .FirstAsync(x => x.Id == form.Id, cancellationToken);

            return (ToResponse(created, prefix), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(FormResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpsertFormRequest request,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var form = await db.Forms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (form is null)
            {
                return (null, "Form not found.", StatusCodes.Status404NotFound);
            }

            var accessError = EnsureCanAccess(form, user);
            if (accessError is not null)
            {
                return (null, accessError, StatusCodes.Status403Forbidden);
            }

            if (await SlugExistsAsync(request.Slug, prefix, excludeFormId: id, cancellationToken))
            {
                return (null, "A form with this slug already exists.", StatusCodes.Status409Conflict);
            }

            form.Update(
                request.Title,
                request.Slug,
                request.Description,
                request.SubmitButtonText,
                request.SuccessMessage,
                request.IsPublished);

            var translation = await db.FormTranslations
                .FirstOrDefaultAsync(x => x.FormId == id && x.LanguagePrefix == prefix, cancellationToken);

            if (translation is null)
            {
                db.FormTranslations.Add(FormTranslation.Create(
                    id,
                    prefix,
                    request.Title,
                    request.Slug,
                    request.Description,
                    request.SubmitButtonText,
                    request.SuccessMessage));
            }
            else
            {
                translation.Update(
                    request.Title,
                    request.Slug,
                    request.Description,
                    request.SubmitButtonText,
                    request.SuccessMessage);
            }

            await db.SaveChangesAsync(cancellationToken);

            var updated = await db.Forms.AsNoTracking()
                .Include(x => x.Translations)
                .Include(x => x.Fields)
                    .ThenInclude(f => f.Translations)
                .FirstAsync(x => x.Id == id, cancellationToken);

            return (ToResponse(updated, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(
        Guid id,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var form = await db.Forms.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (form is null)
        {
            return ("Form not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(form, user);
        if (accessError is not null)
        {
            return (accessError, StatusCodes.Status403Forbidden);
        }

        db.Forms.Remove(form);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(FormResponse? Response, string? Error, int StatusCode)> SyncFieldsAsync(
        Guid formId,
        SyncFormFieldsRequest request,
        ClaimsPrincipal? user,
        string? lang,
        CancellationToken cancellationToken)
    {
        try
        {
            var prefix = LanguagePrefix.Normalize(lang);
            var form = await db.Forms
                .Include(x => x.Fields)
                    .ThenInclude(f => f.Translations)
                .FirstOrDefaultAsync(x => x.Id == formId, cancellationToken);

            if (form is null)
            {
                return (null, "Form not found.", StatusCodes.Status404NotFound);
            }

            var accessError = EnsureCanAccess(form, user);
            if (accessError is not null)
            {
                return (null, accessError, StatusCodes.Status403Forbidden);
            }

            var incoming = request.Fields ?? [];
            var keys = incoming.Select(x => x.FieldKey.Trim().ToLowerInvariant().Replace(' ', '_')).ToList();
            if (keys.Count != keys.Distinct(StringComparer.Ordinal).Count())
            {
                return (null, "Field keys must be unique within a form.", StatusCodes.Status400BadRequest);
            }

            var keepIds = incoming
                .Where(x => x.Id.HasValue && x.Id.Value != Guid.Empty)
                .Select(x => x.Id!.Value)
                .ToHashSet();

            var toRemove = form.Fields.Where(f => !keepIds.Contains(f.Id)).ToList();
            if (toRemove.Count > 0)
            {
                db.FormFields.RemoveRange(toRemove);
            }

            foreach (var item in incoming.OrderBy(x => x.SortOrder))
            {
                var optionsJson = SerializeOptions(item.Options);
                FormField field;

                if (item.Id.HasValue && item.Id.Value != Guid.Empty)
                {
                    field = form.Fields.FirstOrDefault(f => f.Id == item.Id.Value)
                        ?? throw new ArgumentException($"Field {item.Id} was not found on this form.");

                    field.Update(
                        item.FieldKey,
                        item.FieldType,
                        item.Label,
                        item.Placeholder,
                        item.HelpText,
                        optionsJson,
                        item.IsRequired,
                        item.SortOrder);
                }
                else
                {
                    field = FormField.Create(
                        formId,
                        item.FieldKey,
                        item.FieldType,
                        item.Label,
                        item.Placeholder,
                        item.HelpText,
                        optionsJson,
                        item.IsRequired,
                        item.SortOrder);
                    db.FormFields.Add(field);
                    await db.SaveChangesAsync(cancellationToken);
                }

                UpsertFieldTranslation(field, prefix, item.Label, item.Placeholder, item.HelpText);
            }

            form.Touch();
            await db.SaveChangesAsync(cancellationToken);

            var updated = await db.Forms.AsNoTracking()
                .Include(x => x.Translations)
                .Include(x => x.Fields)
                    .ThenInclude(f => f.Translations)
                .FirstAsync(x => x.Id == formId, cancellationToken);

            return (ToResponse(updated, prefix), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(IReadOnlyList<FormSubmissionResponse>? Response, string? Error, int StatusCode)> ListSubmissionsAsync(
        Guid formId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var form = await db.Forms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == formId, cancellationToken);
        if (form is null)
        {
            return (null, "Form not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(form, user);
        if (accessError is not null)
        {
            return (null, accessError, StatusCodes.Status403Forbidden);
        }

        var items = await db.FormSubmissions.AsNoTracking()
            .Where(x => x.FormId == formId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new FormSubmissionResponse(
                x.Id,
                x.FormId,
                x.PayloadJson,
                x.LanguagePrefix,
                x.IpAddress,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return (items, null, StatusCodes.Status200OK);
    }

    public async Task<(string? Error, int StatusCode)> DeleteSubmissionAsync(
        Guid formId,
        Guid submissionId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var form = await db.Forms.AsNoTracking().FirstOrDefaultAsync(x => x.Id == formId, cancellationToken);
        if (form is null)
        {
            return ("Form not found.", StatusCodes.Status404NotFound);
        }

        var accessError = EnsureCanAccess(form, user);
        if (accessError is not null)
        {
            return (accessError, StatusCodes.Status403Forbidden);
        }

        var submission = await db.FormSubmissions
            .FirstOrDefaultAsync(x => x.Id == submissionId && x.FormId == formId, cancellationToken);
        if (submission is null)
        {
            return ("Submission not found.", StatusCodes.Status404NotFound);
        }

        db.FormSubmissions.Remove(submission);
        await db.SaveChangesAsync(cancellationToken);
        return (null, StatusCodes.Status204NoContent);
    }

    public async Task<(PublicFormResponse? Response, string? Error, int StatusCode)> GetPublicBySlugAsync(
        string slug,
        string? lang,
        CancellationToken cancellationToken)
    {
        var prefix = LanguagePrefix.NormalizeOptional(lang);
        var normalizedSlug = slug.Trim().ToLowerInvariant();

        FormDefinition? form = null;
        if (prefix is not null)
        {
            var translation = await db.FormTranslations.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LanguagePrefix == prefix && x.Slug == normalizedSlug, cancellationToken);
            if (translation is not null)
            {
                form = await db.Forms.AsNoTracking()
                    .Include(x => x.Translations)
                    .Include(x => x.Fields)
                        .ThenInclude(f => f.Translations)
                    .FirstOrDefaultAsync(x => x.Id == translation.FormId, cancellationToken);
            }
        }

        form ??= await db.Forms.AsNoTracking()
            .Include(x => x.Translations)
            .Include(x => x.Fields)
                .ThenInclude(f => f.Translations)
            .FirstOrDefaultAsync(x => x.Slug == normalizedSlug, cancellationToken);

        if (form is null || !form.IsPublished)
        {
            return (null, "Form not found.", StatusCodes.Status404NotFound);
        }

        var full = ToResponse(form, prefix);
        return (new PublicFormResponse(
            full.Id,
            full.Title,
            full.Slug,
            full.Description,
            full.SubmitButtonText,
            full.SuccessMessage,
            full.Fields), null, StatusCodes.Status200OK);
    }

    public async Task<(FormSubmissionResponse? Response, string? Error, int StatusCode)> SubmitPublicAsync(
        string slug,
        SubmitFormRequest request,
        string? lang,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var (publicForm, error, statusCode) = await GetPublicBySlugAsync(slug, lang, cancellationToken);
        if (publicForm is null)
        {
            return (null, error, statusCode);
        }

        var values = request.Values ?? new Dictionary<string, string?>();
        foreach (var field in publicForm.Fields)
        {
            values.TryGetValue(field.FieldKey, out var raw);
            var value = raw?.Trim();
            if (field.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                return (null, $"Field '{field.Label}' is required.", StatusCodes.Status400BadRequest);
            }
        }

        var payload = JsonSerializer.Serialize(values, JsonOptions);
        var submission = FormSubmission.Create(
            publicForm.Id,
            payload,
            LanguagePrefix.NormalizeOptional(lang),
            ipAddress,
            userAgent);

        db.FormSubmissions.Add(submission);
        await db.SaveChangesAsync(cancellationToken);

        return (new FormSubmissionResponse(
            submission.Id,
            submission.FormId,
            submission.PayloadJson,
            submission.LanguagePrefix,
            submission.IpAddress,
            submission.CreatedAt), null, StatusCodes.Status201Created);
    }

    private async Task<bool> SlugExistsAsync(
        string slug,
        string languagePrefix,
        Guid? excludeFormId,
        CancellationToken cancellationToken)
    {
        var normalized = slug.Trim().ToLowerInvariant().Replace(' ', '-');
        return await db.FormTranslations.AnyAsync(
            x => x.LanguagePrefix == languagePrefix
                 && x.Slug == normalized
                 && (!excludeFormId.HasValue || x.FormId != excludeFormId.Value),
            cancellationToken);
    }

    private static void UpsertFieldTranslation(
        FormField field,
        string languagePrefix,
        string label,
        string? placeholder,
        string? helpText)
    {
        var existing = field.Translations.FirstOrDefault(t => t.LanguagePrefix == languagePrefix);
        if (existing is null)
        {
            field.Translations.Add(FormFieldTranslation.Create(
                field.Id,
                languagePrefix,
                label,
                placeholder,
                helpText));
        }
        else
        {
            existing.Update(label, placeholder, helpText);
        }
    }

    private static string? EnsureCanAccess(FormDefinition form, ClaimsPrincipal? user)
    {
        if (IsAdmin(user))
        {
            return null;
        }

        var userId = TryGetUserId(user);
        if (!userId.HasValue)
        {
            return "Authentication required.";
        }

        if (form.CreatedBy.HasValue && form.CreatedBy.Value != userId.Value)
        {
            return "You can only manage forms you created.";
        }

        return null;
    }

    private static (string Title, string Slug, string? LanguagePrefix) ResolveListFields(
        FormDefinition form,
        string? prefix)
    {
        if (prefix is not null)
        {
            var translation = form.Translations.FirstOrDefault(t => t.LanguagePrefix == prefix);
            if (translation is not null)
            {
                return (translation.Title, translation.Slug, prefix);
            }
        }

        return (form.Title, form.Slug, prefix);
    }

    private static FormResponse ToResponse(FormDefinition form, string? prefix)
    {
        string title = form.Title;
        string slug = form.Slug;
        string? description = form.Description;
        string submitButtonText = form.SubmitButtonText;
        string? successMessage = form.SuccessMessage;
        string? languagePrefix = prefix;

        if (prefix is not null)
        {
            var translation = form.Translations.FirstOrDefault(t => t.LanguagePrefix == prefix);
            if (translation is not null)
            {
                title = translation.Title;
                slug = translation.Slug;
                description = translation.Description;
                submitButtonText = translation.SubmitButtonText;
                successMessage = translation.SuccessMessage;
            }
        }

        var fields = form.Fields
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.CreatedAt)
            .Select(f => ToFieldResponse(f, prefix))
            .ToList();

        return new FormResponse(
            form.Id,
            title,
            slug,
            description,
            submitButtonText,
            successMessage,
            form.IsPublished,
            form.CreatedBy,
            form.CreatedAt,
            form.UpdatedAt,
            languagePrefix,
            fields);
    }

    private static FormFieldResponse ToFieldResponse(FormField field, string? prefix)
    {
        var label = field.Label;
        var placeholder = field.Placeholder;
        var helpText = field.HelpText;

        if (prefix is not null)
        {
            var translation = field.Translations.FirstOrDefault(t => t.LanguagePrefix == prefix);
            if (translation is not null)
            {
                label = translation.Label;
                placeholder = translation.Placeholder;
                helpText = translation.HelpText;
            }
        }

        return new FormFieldResponse(
            field.Id,
            field.FieldKey,
            field.FieldType,
            label,
            placeholder,
            helpText,
            DeserializeOptions(field.OptionsJson),
            field.IsRequired,
            field.SortOrder);
    }

    private static string? SerializeOptions(IReadOnlyList<string>? options)
    {
        if (options is null || options.Count == 0)
        {
            return null;
        }

        var cleaned = options
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return cleaned.Count == 0 ? null : JsonSerializer.Serialize(cleaned, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(optionsJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static Guid? TryGetUserId(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? principal.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static bool IsAdmin(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return false;
        }

        var role = principal.FindFirstValue(ClaimTypes.Role)
                   ?? principal.FindFirstValue("role");
        return string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
    }
}
