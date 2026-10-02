using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.Tickets.Infrastructure;

public static class TicketsSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TicketsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("TicketsSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'tickets')
                EXEC(N'CREATE SCHEMA [tickets]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[tickets].[Tickets]', N'U') IS NULL
            BEGIN
                CREATE TABLE [tickets].[Tickets]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Tickets_id] DEFAULT (NEWSEQUENTIALID()),
                    [user_id] UNIQUEIDENTIFIER NOT NULL,
                    [author_name] NVARCHAR(200) NOT NULL,
                    [author_email] NVARCHAR(256) NOT NULL,
                    [category] NVARCHAR(40) NOT NULL,
                    [title] NVARCHAR(200) NOT NULL,
                    [body] NVARCHAR(MAX) NOT NULL,
                    [status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Tickets_status] DEFAULT (N'open'),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_Tickets] PRIMARY KEY ([id])
                );

                CREATE INDEX [IX_Tickets_user_id] ON [tickets].[Tickets] ([user_id]);
                CREATE INDEX [IX_Tickets_status] ON [tickets].[Tickets] ([status]);
                CREATE INDEX [IX_Tickets_category] ON [tickets].[Tickets] ([category]);
                CREATE INDEX [IX_Tickets_updated_at] ON [tickets].[Tickets] ([updated_at]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[tickets].[TicketReplies]', N'U') IS NULL
            BEGIN
                CREATE TABLE [tickets].[TicketReplies]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_TicketReplies_id] DEFAULT (NEWSEQUENTIALID()),
                    [ticket_id] UNIQUEIDENTIFIER NOT NULL,
                    [author_user_id] UNIQUEIDENTIFIER NOT NULL,
                    [author_name] NVARCHAR(200) NOT NULL,
                    [from_staff] BIT NOT NULL CONSTRAINT [DF_TicketReplies_staff] DEFAULT (0),
                    [body] NVARCHAR(MAX) NOT NULL,
                    [created_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_TicketReplies] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_TicketReplies_Tickets]
                        FOREIGN KEY ([ticket_id]) REFERENCES [tickets].[Tickets] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_TicketReplies_ticket_id] ON [tickets].[TicketReplies] ([ticket_id]);
                CREATE INDEX [IX_TicketReplies_created_at] ON [tickets].[TicketReplies] ([created_at]);
            END
            """, cancellationToken);

        logger.LogInformation("Tickets schema ensured.");
    }
}
