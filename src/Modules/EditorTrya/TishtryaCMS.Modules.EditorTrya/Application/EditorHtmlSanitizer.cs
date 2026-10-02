using System.Text.RegularExpressions;

namespace TishtryaCMS.Modules.EditorTrya.Application;

public static partial class EditorHtmlSanitizer
{
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var value = html;

        value = ScriptTagRegex().Replace(value, string.Empty);
        value = StyleTagRegex().Replace(value, string.Empty);
        value = IframeTagRegex().Replace(value, string.Empty);
        value = ObjectEmbedRegex().Replace(value, string.Empty);
        value = EventHandlerRegex().Replace(value, string.Empty);
        value = JavascriptUrlRegex().Replace(value, "href=\"#\"");
        value = DataUrlScriptRegex().Replace(value, string.Empty);

        return value.Trim();
    }

    [GeneratedRegex(@"<\s*script[^>]*>.*?<\s*/\s*script\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"<\s*style[^>]*>.*?<\s*/\s*style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex StyleTagRegex();

    [GeneratedRegex(@"<\s*iframe[^>]*>.*?<\s*/\s*iframe\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex IframeTagRegex();

    [GeneratedRegex(@"<\s*(object|embed|applet)[^>]*>.*?<\s*/\s*\1\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ObjectEmbedRegex();

    [GeneratedRegex(@"\son\w+\s*=\s*(""[^""]*""|'[^']*'|[^\s>]+)", RegexOptions.IgnoreCase)]
    private static partial Regex EventHandlerRegex();

    [GeneratedRegex(@"href\s*=\s*[""']\s*javascript:[^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptUrlRegex();

    [GeneratedRegex(@"src\s*=\s*[""']\s*data:text/html[^""']*[""']", RegexOptions.IgnoreCase)]
    private static partial Regex DataUrlScriptRegex();
}
