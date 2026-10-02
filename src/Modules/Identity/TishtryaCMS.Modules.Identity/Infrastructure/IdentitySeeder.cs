using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TishtryaCMS.Modules.Identity.Domain;

namespace TishtryaCMS.Modules.Identity.Infrastructure;

public static class IdentitySeeder
{
    public const string SuperAdminEmail = "info@tishtrya.cp";
    public const string SuperAdminPassword = "essi36865";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentitySeeder");

        await db.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureOtpTableAsync(db, cancellationToken);

        var email = SuperAdminEmail.ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return;
        }

        var hasher = new PasswordHasher<User>();
        var placeholder = User.Create(SuperAdminEmail, "pending", Roles.SuperAdmin, "Tishtrya Super Admin");
        var passwordHash = hasher.HashPassword(placeholder, SuperAdminPassword);
        var user = User.Create(
            email: SuperAdminEmail,
            passwordHash: passwordHash,
            role: Roles.SuperAdmin,
            displayName: "Tishtrya Super Admin");

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Seeded SuperAdmin user {Email}", SuperAdminEmail);
    }

    private static async Task EnsureOtpTableAsync(IdentityDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[identity].[OtpCodes]', N'U') IS NULL
            BEGIN
                CREATE TABLE [identity].[OtpCodes]
                (
                    [Id] UNIQUEIDENTIFIER NOT NULL,
                    [UserId] UNIQUEIDENTIFIER NOT NULL,
                    [CodeHash] NVARCHAR(128) NOT NULL,
                    [Purpose] NVARCHAR(64) NOT NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [ExpiresAtUtc] DATETIME2 NOT NULL,
                    [ConsumedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_OtpCodes] PRIMARY KEY ([Id])
                );

                CREATE INDEX [IX_OtpCodes_UserId_Purpose] ON [identity].[OtpCodes] ([UserId], [Purpose]);
                CREATE INDEX [IX_OtpCodes_ExpiresAtUtc] ON [identity].[OtpCodes] ([ExpiresAtUtc]);
            END
            """, cancellationToken);
    }
}
