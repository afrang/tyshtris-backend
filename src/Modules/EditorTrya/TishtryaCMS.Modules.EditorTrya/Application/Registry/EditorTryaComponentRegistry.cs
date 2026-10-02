using System.Text.Json;

namespace TishtryaCMS.Modules.EditorTrya.Application.Registry;

public sealed class EditorTryaComponentRegistry
{
    private readonly Dictionary<string, EditorComponentDefinition> _definitions;

    public EditorTryaComponentRegistry()
    {
        _definitions = Build().ToDictionary(x => x.Type, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<EditorComponentDefinition> All => _definitions.Values;

    public bool TryGet(string type, out EditorComponentDefinition? definition) =>
        _definitions.TryGetValue(type.Trim().ToLowerInvariant(), out definition);

    public EditorComponentDefinition GetRequired(string type)
    {
        if (!TryGet(type, out var definition) || definition is null)
        {
            throw new ArgumentException($"Unsupported EditorTrya component type '{type}'.", nameof(type));
        }

        return definition;
    }

    private static IEnumerable<EditorComponentDefinition> Build()
    {
        yield return Def(
            "title",
            "Title",
            """{"text":""}""",
            """{"headingType":"h2","color":"#111111","fontSize":"32px","fontWeight":700,"textAlign":"left"}""",
            ValidateTitleData,
            ValidateTitleOptions);

        yield return Def(
            "text",
            "Text",
            """{"html":""}""",
            """{"color":"#333333","fontSize":"16px","lineHeight":1.8}""",
            ValidateTextData,
            _ => null,
            usesHtml: true);

        yield return Def(
            "image",
            "Image",
            """{"fileId":null,"alt":"","title":null,"link":null}""",
            """{"objectFit":"cover","borderRadius":"0px","alignment":"center","width":"100%","maxWidth":"100%","height":"auto","display":"block"}""",
            ValidateImageData,
            _ => null,
            fileManagerComponent: "editortryaimage");

        yield return Def(
            "gallery",
            "Gallery",
            """{"layout":"carousel","columns":3,"slidesPerView":1}""",
            """{"gap":"12px","slidesPerView":1,"showArrows":true,"showDots":true}""",
            ValidateGalleryData,
            _ => null,
            fileManagerComponent: "editortryagallery");

        yield return Def(
            "button",
            "Button",
            """{"text":"Read More","url":"#","target":"_self"}""",
            """{"size":"medium","alignment":"left","borderRadius":"6px","backgroundColor":"#9a55f0","color":"#ffffff","shadow":"none","boxShadow":""}""",
            ValidateButtonData,
            _ => null);

        yield return Def(
            "video",
            "Video And Audio",
            """{"sourceType":"youtube","url":"","fileId":null,"mediaKind":null}""",
            """{"aspectRatio":"16/9"}""",
            ValidateVideoData,
            _ => null,
            fileManagerComponent: "editortryavideoaudio");

        yield return Def(
            "divider",
            "Divider",
            """{"text":""}""",
            """{"style":"solid","width":"100%","thickness":"1px","color":"#d5d7e2","contentMode":"none","icon":"star","text":"","contentColor":"#6f7280","gap":"12px"}""",
            _ => null,
            _ => null);

        yield return Def(
            "spacer",
            "Spacer",
            """{}""",
            """{"height":{"desktop":"50px","tablet":"35px","mobile":"20px"}}""",
            _ => null,
            _ => null);

        yield return Def(
            "quote",
            "Quote",
            """{"text":"","cite":null}""",
            """{"fontStyle":"italic","textAlign":"left"}""",
            ValidateQuoteData,
            _ => null);

        yield return Def(
            "html",
            "HTML",
            """{"html":""}""",
            """{}""",
            ValidateTextData,
            _ => null,
            usesHtml: true);
    }

    private static EditorComponentDefinition Def(
        string type,
        string name,
        string defaultData,
        string defaultOptions,
        Func<JsonElement?, string?> validateData,
        Func<JsonElement?, string?> validateOptions,
        bool usesHtml = false,
        string? fileManagerComponent = null) =>
        new()
        {
            Type = type,
            Name = name,
            DefaultDataJson = defaultData,
            DefaultOptionsJson = defaultOptions,
            ValidateData = validateData,
            ValidateOptions = validateOptions,
            UsesHtml = usesHtml,
            FileManagerComponent = fileManagerComponent
        };

    private static string? ValidateTitleData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Title data must be an object.";
        }

        if (!data.Value.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
        {
            return "Title data.text is required.";
        }

        return null;
    }

    private static string? ValidateTitleOptions(JsonElement? options)
    {
        if (options is null || options.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (options.Value.TryGetProperty("headingType", out var heading))
        {
            var value = heading.GetString()?.ToLowerInvariant();
            if (value is not ("h1" or "h2" or "h3" or "h4" or "h5" or "h6"))
            {
                return "headingType must be h1-h6.";
            }
        }

        return null;
    }

    private static string? ValidateTextData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Text/HTML data must be an object.";
        }

        if (!data.Value.TryGetProperty("html", out var html) || html.ValueKind != JsonValueKind.String)
        {
            return "data.html is required.";
        }

        return null;
    }

    private static string? ValidateImageData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Image data must be an object.";
        }

        return null;
    }

    private static string? ValidateGalleryData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Gallery data must be an object.";
        }

        return null;
    }

    private static string? ValidateButtonData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Button data must be an object.";
        }

        if (!data.Value.TryGetProperty("text", out var text) || text.ValueKind != JsonValueKind.String)
        {
            return "Button data.text is required.";
        }

        if (data.Value.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
        {
            var value = url.GetString() ?? string.Empty;
            if (value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                return "Button URL cannot use javascript: protocol.";
            }
        }

        return null;
    }

    private static string? ValidateVideoData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Video And Audio data must be an object.";
        }

        if (data.Value.TryGetProperty("sourceType", out var source))
        {
            var value = source.GetString()?.ToLowerInvariant();
            if (value is not ("youtube" or "file" or "external"))
            {
                return "sourceType must be 'youtube' or 'file'.";
            }
        }

        return null;
    }

    private static string? ValidateQuoteData(JsonElement? data)
    {
        if (data is null || data.Value.ValueKind != JsonValueKind.Object)
        {
            return "Quote data must be an object.";
        }

        return null;
    }

}
