using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Settings.Domain;
using TishtryaCMS.Modules.Settings.Infrastructure;

namespace TishtryaCMS.Modules.Settings.Application;

public sealed class LanguageService(SettingsDbContext db)
{
    public async Task<IReadOnlyList<LanguageResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await db.Languages
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.Name)
            .Select(x => new LanguageResponse(x.Id, x.Name, x.Prefix, x.IsDefault, x.Direction))
            .ToListAsync(cancellationToken);
    }

    public async Task<(LanguageResponse? Response, string? Error, int StatusCode)> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var language = await db.Languages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (language is null)
        {
            return (null, "Language not found.", StatusCodes.Status404NotFound);
        }

        return (ToResponse(language), null, StatusCodes.Status200OK);
    }

    public async Task<(LanguageResponse? Response, string? Error, int StatusCode)> CreateAsync(
        CreateLanguageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (await PrefixExistsAsync(request.Prefix, excludeId: null, cancellationToken))
            {
                return (null, "A language with this prefix already exists.", StatusCodes.Status409Conflict);
            }

            var makeDefault = request.IsDefault || !await db.Languages.AnyAsync(cancellationToken);
            if (makeDefault)
            {
                await ClearDefaultsAsync(cancellationToken);
            }

            var language = Language.Create(request.Name, request.Prefix, makeDefault, request.Direction);
            db.Languages.Add(language);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(language), null, StatusCodes.Status201Created);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(LanguageResponse? Response, string? Error, int StatusCode)> UpdateAsync(
        Guid id,
        UpdateLanguageRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var language = await db.Languages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (language is null)
            {
                return (null, "Language not found.", StatusCodes.Status404NotFound);
            }

            if (await PrefixExistsAsync(request.Prefix, excludeId: id, cancellationToken))
            {
                return (null, "A language with this prefix already exists.", StatusCodes.Status409Conflict);
            }

            var makeDefault = request.IsDefault;
            if (!makeDefault && language.IsDefault)
            {
                // Keep at least one default language.
                makeDefault = true;
            }

            if (makeDefault)
            {
                await ClearDefaultsAsync(cancellationToken);
            }

            language.Update(request.Name, request.Prefix, makeDefault, request.Direction);
            await db.SaveChangesAsync(cancellationToken);
            return (ToResponse(language), null, StatusCodes.Status200OK);
        }
        catch (ArgumentException ex)
        {
            return (null, ex.Message, StatusCodes.Status400BadRequest);
        }
    }

    public async Task<(string? Error, int StatusCode)> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var language = await db.Languages.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (language is null)
        {
            return ("Language not found.", StatusCodes.Status404NotFound);
        }

        var wasDefault = language.IsDefault;
        db.Languages.Remove(language);
        await db.SaveChangesAsync(cancellationToken);

        if (wasDefault)
        {
            var next = await db.Languages.OrderBy(x => x.Name).FirstOrDefaultAsync(cancellationToken);
            if (next is not null)
            {
                next.SetDefault(true);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return (null, StatusCodes.Status204NoContent);
    }

    private async Task ClearDefaultsAsync(CancellationToken cancellationToken)
    {
        var defaults = await db.Languages.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var item in defaults)
        {
            item.SetDefault(false);
        }
    }

    private async Task<bool> PrefixExistsAsync(string prefix, Guid? excludeId, CancellationToken cancellationToken)
    {
        var normalized = prefix.Trim().ToLowerInvariant().Replace('_', '-');
        return await db.Languages.AnyAsync(
            x => x.Prefix == normalized && (!excludeId.HasValue || x.Id != excludeId.Value),
            cancellationToken);
    }

    private static LanguageResponse ToResponse(Language language) =>
        new(language.Id, language.Name, language.Prefix, language.IsDefault, language.Direction);
}
