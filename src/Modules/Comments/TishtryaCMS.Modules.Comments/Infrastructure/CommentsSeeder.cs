using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.Comments.Infrastructure;

public static class CommentsSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommentsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("CommentsSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'comments')
                EXEC(N'CREATE SCHEMA [comments]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[comments].[Comments]', N'U') IS NULL
            BEGIN
                CREATE TABLE [comments].[Comments]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Comments_id] DEFAULT (NEWSEQUENTIALID()),
                    [component] NVARCHAR(100) NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NOT NULL,
                    [parent_comment_id] UNIQUEIDENTIFIER NULL,
                    [user_id] UNIQUEIDENTIFIER NOT NULL,
                    [author_display_name] NVARCHAR(200) NOT NULL,
                    [author_email] NVARCHAR(256) NULL,
                    [body] NVARCHAR(MAX) NOT NULL,
                    [status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Comments_status] DEFAULT (N'pending'),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_Comments] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_Comments_ParentComment]
                        FOREIGN KEY ([parent_comment_id]) REFERENCES [comments].[Comments] ([id])
                );

                CREATE INDEX [IX_Comments_component_parent_id]
                    ON [comments].[Comments] ([component], [parent_id]);
                CREATE INDEX [IX_Comments_component_parent_id_status]
                    ON [comments].[Comments] ([component], [parent_id], [status]);
                CREATE INDEX [IX_Comments_user_id]
                    ON [comments].[Comments] ([user_id]);
                CREATE INDEX [IX_Comments_parent_comment_id]
                    ON [comments].[Comments] ([parent_comment_id]);
                CREATE INDEX [IX_Comments_created_at]
                    ON [comments].[Comments] ([created_at]);
            END
            """, cancellationToken);

        logger.LogInformation("Comments schema ensured.");
    }
}
