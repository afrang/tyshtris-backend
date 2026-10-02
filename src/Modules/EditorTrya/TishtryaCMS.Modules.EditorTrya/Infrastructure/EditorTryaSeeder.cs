using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.EditorTrya.Infrastructure;

public static class EditorTryaSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EditorTryaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("EditorTryaSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'editortrya')
                EXEC(N'CREATE SCHEMA [editortrya]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[editortrya].[EditorTryaContents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [editortrya].[EditorTryaContents]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_EditorTryaContents_id] DEFAULT (NEWSEQUENTIALID()),
                    [component] NVARCHAR(100) NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [publish] BIT NOT NULL CONSTRAINT [DF_EditorTryaContents_publish] DEFAULT (1),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NULL,
                    CONSTRAINT [PK_EditorTryaContents] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_EditorTryaContents_component_parent_lang] UNIQUE ([component], [parent_id], [language_prefix])
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH(N'editortrya.EditorTryaContents', N'language_prefix') IS NULL
            BEGIN
                ALTER TABLE [editortrya].[EditorTryaContents]
                    ADD [language_prefix] NVARCHAR(20) NULL;
            END
            """, cancellationToken);

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

            UPDATE [editortrya].[EditorTryaContents]
            SET [language_prefix] = @lang
            WHERE [language_prefix] IS NULL OR LTRIM(RTRIM([language_prefix])) = N'';
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (
                SELECT 1
                FROM sys.columns
                WHERE object_id = OBJECT_ID(N'[editortrya].[EditorTryaContents]')
                  AND name = N'language_prefix'
                  AND is_nullable = 1)
            BEGIN
                ALTER TABLE [editortrya].[EditorTryaContents]
                    ALTER COLUMN [language_prefix] NVARCHAR(20) NOT NULL;
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF EXISTS (
                SELECT 1
                FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'[editortrya].[EditorTryaContents]')
                  AND name = N'UQ_EditorTryaContents_component_parent')
            BEGIN
                ALTER TABLE [editortrya].[EditorTryaContents]
                    DROP CONSTRAINT [UQ_EditorTryaContents_component_parent];
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (
                SELECT 1
                FROM sys.key_constraints
                WHERE parent_object_id = OBJECT_ID(N'[editortrya].[EditorTryaContents]')
                  AND name = N'UQ_EditorTryaContents_component_parent_lang')
            BEGIN
                ALTER TABLE [editortrya].[EditorTryaContents]
                    ADD CONSTRAINT [UQ_EditorTryaContents_component_parent_lang]
                        UNIQUE ([component], [parent_id], [language_prefix]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[editortrya].[EditorTryaContainers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [editortrya].[EditorTryaContainers]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_EditorTryaContainers_id] DEFAULT (NEWSEQUENTIALID()),
                    [content_id] UNIQUEIDENTIFIER NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NULL,
                    [component] NVARCHAR(100) NULL,
                    [cols] INT NULL,
                    [ordered] INT NOT NULL CONSTRAINT [DF_EditorTryaContainers_ordered] DEFAULT (1),
                    [publish] BIT NOT NULL CONSTRAINT [DF_EditorTryaContainers_publish] DEFAULT (1),
                    [options] NVARCHAR(MAX) NULL,
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NULL,
                    CONSTRAINT [PK_EditorTryaContainers] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_EditorTryaContainers_Contents]
                        FOREIGN KEY ([content_id]) REFERENCES [editortrya].[EditorTryaContents] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_EditorTryaContainers_content_id]
                    ON [editortrya].[EditorTryaContainers] ([content_id]);
                CREATE INDEX [IX_EditorTryaContainers_content_id_ordered]
                    ON [editortrya].[EditorTryaContainers] ([content_id], [ordered]);
                CREATE INDEX [IX_EditorTryaContainers_parent_id]
                    ON [editortrya].[EditorTryaContainers] ([parent_id]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[editortrya].[EditorTryaComponents]', N'U') IS NULL
            BEGIN
                CREATE TABLE [editortrya].[EditorTryaComponents]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_EditorTryaComponents_id] DEFAULT (NEWSEQUENTIALID()),
                    [container_id] UNIQUEIDENTIFIER NOT NULL,
                    [type] NVARCHAR(100) NOT NULL,
                    [ordered] INT NOT NULL CONSTRAINT [DF_EditorTryaComponents_ordered] DEFAULT (1),
                    [publish] BIT NOT NULL CONSTRAINT [DF_EditorTryaComponents_publish] DEFAULT (1),
                    [data] NVARCHAR(MAX) NULL,
                    [options] NVARCHAR(MAX) NULL,
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NULL,
                    CONSTRAINT [PK_EditorTryaComponents] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_EditorTryaComponents_Containers]
                        FOREIGN KEY ([container_id]) REFERENCES [editortrya].[EditorTryaContainers] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_EditorTryaComponents_container_id]
                    ON [editortrya].[EditorTryaComponents] ([container_id]);
                CREATE INDEX [IX_EditorTryaComponents_container_id_ordered]
                    ON [editortrya].[EditorTryaComponents] ([container_id], [ordered]);
                CREATE INDEX [IX_EditorTryaComponents_type]
                    ON [editortrya].[EditorTryaComponents] ([type]);
                CREATE INDEX [IX_EditorTryaComponents_publish]
                    ON [editortrya].[EditorTryaComponents] ([publish]);
            END
            """, cancellationToken);

        logger.LogInformation("EditorTrya schema ensured.");
    }
}
