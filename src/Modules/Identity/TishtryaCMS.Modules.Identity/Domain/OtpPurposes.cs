namespace TishtryaCMS.Modules.Identity.Domain;

public static class OtpPurposes
{
    public const string EmailVerification = "EmailVerification";
    public const string Login = "Login";

    public static bool IsKnown(string? purpose) =>
        purpose is EmailVerification or Login;
}
