using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TishtryaCMS.Modules.Settings.Domain;

namespace TishtryaCMS.Modules.Settings.Infrastructure;

public static class SettingsSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SettingsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SettingsSeeder");

        await EnsureSettingsTablesAsync(db, logger, cancellationToken);

        if (!await db.SiteSettings.AnyAsync(cancellationToken))
        {
            db.SiteSettings.Add(SiteSettings.CreateDefault());
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default site settings seeded.");
        }

        await SeedDefaultLanguagesAsync(db, logger, cancellationToken);
        await MigrateSiteSettingsTranslationsAsync(db, cancellationToken);
        logger.LogInformation("Settings module schema ensured (SiteSettings, Languages, SiteSettingsTranslations).");
    }

    private static async Task SeedDefaultLanguagesAsync(
        SettingsDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (!await db.Languages.AnyAsync(cancellationToken))
        {
            db.Languages.Add(Language.Create("English", "en", isDefault: true, TextDirections.Ltr));
            db.Languages.Add(Language.Create("فارسی", "fa", isDefault: false, TextDirections.Rtl));
            db.Languages.Add(Language.Create("العربية", "ar", isDefault: false, TextDirections.Rtl));
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default languages seeded (en, fa, ar).");
            return;
        }

        // Prefer native labels for known prefixes.
        var persian = await db.Languages.FirstOrDefaultAsync(x => x.Prefix == "fa", cancellationToken);
        if (persian is not null &&
            (string.Equals(persian.Name, "Persian", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(persian.Name, "Farsi", StringComparison.OrdinalIgnoreCase)))
        {
            persian.Update("فارسی", persian.Prefix, persian.IsDefault, persian.Direction);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Updated Persian language display name to فارسی.");
        }
    }

    private static async Task EnsureSettingsTablesAsync(
        SettingsDbContext db,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'settings')
                EXEC(N'CREATE SCHEMA [settings]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[SiteSettings]', N'U') IS NULL
            BEGIN
                CREATE TABLE [settings].[SiteSettings]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [title] NVARCHAR(255) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [logo_url] NVARCHAR(1000) NULL,
                    [social_links_json] NVARCHAR(MAX) NOT NULL,
                    [contact_addresses_json] NVARCHAR(MAX) NOT NULL,
                    [contact_phones_json] NVARCHAR(MAX) NOT NULL,
                    [contact_emails_json] NVARCHAR(MAX) NOT NULL,
                    [storage_provider] NVARCHAR(20) NOT NULL,
                    [s3_endpoint] NVARCHAR(500) NULL,
                    [s3_bucket] NVARCHAR(200) NULL,
                    [s3_access_key] NVARCHAR(500) NULL,
                    [s3_secret_key] NVARCHAR(500) NULL,
                    [s3_region] NVARCHAR(100) NULL,
                    [s3_public_base_url] NVARCHAR(500) NULL,
                    [updated_at_utc] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_SiteSettings] PRIMARY KEY ([id])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [settings].[Languages]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Languages_id] DEFAULT (NEWSEQUENTIALID()),
                    [name] NVARCHAR(100) NOT NULL,
                    [prefix] NVARCHAR(20) NOT NULL,
                    [is_default] BIT NOT NULL CONSTRAINT [DF_Languages_is_default] DEFAULT (0),
                    [direction] NVARCHAR(10) NOT NULL CONSTRAINT [DF_Languages_direction] DEFAULT (N'ltr'),
                    CONSTRAINT [PK_Languages] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_Languages_prefix] UNIQUE ([prefix])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NOT NULL
               AND COL_LENGTH(N'settings.Languages', N'is_default') IS NULL
            BEGIN
                ALTER TABLE [settings].[Languages]
                    ADD [is_default] BIT NOT NULL
                        CONSTRAINT [DF_Languages_is_default] DEFAULT (0);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NOT NULL
               AND COL_LENGTH(N'settings.Languages', N'direction') IS NULL
            BEGIN
                ALTER TABLE [settings].[Languages]
                    ADD [direction] NVARCHAR(10) NOT NULL
                        CONSTRAINT [DF_Languages_direction] DEFAULT (N'ltr');
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [settings].[Languages] WHERE [is_default] = 1)
               AND EXISTS (SELECT 1 FROM [settings].[Languages])
            BEGIN
                ;WITH first_lang AS (
                    SELECT TOP (1) [id]
                    FROM [settings].[Languages]
                    ORDER BY [name]
                )
                UPDATE l
                SET l.[is_default] = 1
                FROM [settings].[Languages] l
                INNER JOIN first_lang f ON f.[id] = l.[id];
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[settings].[SiteSettingsTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [settings].[SiteSettingsTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_SiteSettingsTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [site_settings_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [name] NVARCHAR(200) NOT NULL,
                    [title] NVARCHAR(255) NOT NULL,
                    [keyword] NVARCHAR(500) NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [contact_addresses_json] NVARCHAR(MAX) NOT NULL,
                    CONSTRAINT [PK_SiteSettingsTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_SiteSettingsTranslations_settings_lang] UNIQUE ([site_settings_id], [language_prefix]),
                    CONSTRAINT [FK_SiteSettingsTranslations_SiteSettings_site_settings_id]
                        FOREIGN KEY ([site_settings_id]) REFERENCES [settings].[SiteSettings] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        logger.LogInformation("Settings module base tables ensured.");
    }

    private static async Task MigrateSiteSettingsTranslationsAsync(
        SettingsDbContext db,
        CancellationToken cancellationToken)
    {
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @lang NVARCHAR(20) = N'en';
            IF OBJECT_ID(N'[settings].[Languages]', N'U') IS NOT NULL
            BEGIN
                SELECT TOP (1) @lang = LOWER(REPLACE(LTRIM(RTRIM([prefix])), N'_', N'-'))
                FROM [settings].[Languages]
                ORDER BY [name];

                IF @lang IS NULL OR LTRIM(RTRIM(@lang)) = N''
                    SET @lang = N'en';
            END

            INSERT INTO [settings].[SiteSettingsTranslations]
                ([site_settings_id], [language_prefix], [name], [title], [keyword], [description], [contact_addresses_json])
            SELECT
                s.[id], @lang, s.[name], s.[title], s.[keyword], s.[description], s.[contact_addresses_json]
            FROM [settings].[SiteSettings] s
            WHERE NOT EXISTS (
                SELECT 1 FROM [settings].[SiteSettingsTranslations] t WHERE t.[site_settings_id] = s.[id]
            );
            """, cancellationToken);
    }
}
