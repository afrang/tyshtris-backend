namespace TishtryaCMS.Modules.Forms.Domain;

public static class FormFieldTypes
{
    public const string Text = "text";
    public const string Email = "email";
    public const string Tel = "tel";
    public const string Number = "number";
    public const string Textarea = "textarea";
    public const string Select = "select";
    public const string Radio = "radio";
    public const string Checkbox = "checkbox";
    public const string Date = "date";
    public const string Url = "url";

    private static readonly HashSet<string> Valid = new(StringComparer.OrdinalIgnoreCase)
    {
        Text, Email, Tel, Number, Textarea, Select, Radio, Checkbox, Date, Url
    };

    public static bool IsValid(string value) => Valid.Contains(value);

    public static bool NeedsOptions(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized is Select or Radio;
    }
}
