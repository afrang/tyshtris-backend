namespace TishtryaCMS.Modules.Identity.Options;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "TishtryaCMS";
    public string Audience { get; set; } = "TishtryaControlCenter";
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 480;
}
