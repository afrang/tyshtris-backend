namespace TishtryaCMS.Modules.Qa.Application;

internal static class LanguagePrefix
{
    public static string Normalize(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
        {
            throw new ArgumentException("lang is required.", nameof(lang));
        }

        return lang.Trim().ToLowerInvariant().Replace('_', '-');
    }
}
