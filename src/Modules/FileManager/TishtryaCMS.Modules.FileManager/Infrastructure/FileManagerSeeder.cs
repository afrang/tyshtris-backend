using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.FileManager.Infrastructure;

public static class FileManagerSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileManagerDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FileManagerSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'filemanager')
                EXEC(N'CREATE SCHEMA [filemanager]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[filemanager].[FileManagers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [filemanager].[FileManagers]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FileManagers_id] DEFAULT (NEWSEQUENTIALID()),
                    [component] NVARCHAR(100) NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NULL,
                    [ordered] INT NOT NULL CONSTRAINT [DF_FileManagers_ordered] DEFAULT (1),
                    [publish] BIT NOT NULL CONSTRAINT [DF_FileManagers_publish] DEFAULT (1),
                    [folder] NVARCHAR(500) NULL,
                    [filename] NVARCHAR(255) NOT NULL,
                    [extension] NVARCHAR(20) NOT NULL,
                    [full_address] NVARCHAR(1000) NOT NULL,
                    [namefile] NVARCHAR(255) NULL,
                    [created_at] DATETIME NOT NULL CONSTRAINT [DF_FileManagers_created_at] DEFAULT (GETDATE()),
                    [updated_at] DATETIME NULL,
                    CONSTRAINT [PK_FileManagers] PRIMARY KEY ([id])
                );

                CREATE INDEX [IX_FileManagers_component_parent_id]
                    ON [filemanager].[FileManagers] ([component], [parent_id]);

                CREATE INDEX [IX_FileManagers_component_parent_id_ordered]
                    ON [filemanager].[FileManagers] ([component], [parent_id], [ordered]);

                CREATE INDEX [IX_FileManagers_filename]
                    ON [filemanager].[FileManagers] ([filename]);

                CREATE INDEX [IX_FileManagers_publish]
                    ON [filemanager].[FileManagers] ([publish]);
            END
            """, cancellationToken);

        logger.LogInformation("FileManager schema ensured.");
    }
}
