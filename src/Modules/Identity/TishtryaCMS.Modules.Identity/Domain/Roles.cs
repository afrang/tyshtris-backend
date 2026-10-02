namespace TishtryaCMS.Modules.Identity.Domain;

public static class Roles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [SuperAdmin, Admin, User];
    public static readonly string[] Assignable = [Admin, User];
    public static readonly string[] AdminRoles = [SuperAdmin, Admin];

    public static bool IsAdmin(string? role) =>
        role is SuperAdmin or Admin;

    public static bool IsKnown(string? role) =>
        role is SuperAdmin or Admin or User;

    public static bool IsAssignable(string? role) =>
        role is Admin or User;
}
