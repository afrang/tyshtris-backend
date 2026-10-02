using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TishtryaCMS.Modules.Qa.Infrastructure;

public static class QaSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QaDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("QaSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'qa')
                EXEC(N'CREATE SCHEMA [qa]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[qa].[QaQuestions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [qa].[QaQuestions]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_QaQuestions_id] DEFAULT (NEWSEQUENTIALID()),
                    [component] NVARCHAR(100) NOT NULL,
                    [parent_id] UNIQUEIDENTIFIER NOT NULL,
                    [ordered] INT NOT NULL CONSTRAINT [DF_QaQuestions_ordered] DEFAULT (1),
                    [publish] BIT NOT NULL CONSTRAINT [DF_QaQuestions_publish] DEFAULT (1),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NULL,
                    CONSTRAINT [PK_QaQuestions] PRIMARY KEY ([id])
                );

                CREATE INDEX [IX_QaQuestions_component_parent_id]
                    ON [qa].[QaQuestions] ([component], [parent_id]);
                CREATE INDEX [IX_QaQuestions_component_parent_id_ordered]
                    ON [qa].[QaQuestions] ([component], [parent_id], [ordered]);
                CREATE INDEX [IX_QaQuestions_publish]
                    ON [qa].[QaQuestions] ([publish]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[qa].[QaQuestionTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [qa].[QaQuestionTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_QaQuestionTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [question_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [question_text] NVARCHAR(2000) NOT NULL,
                    CONSTRAINT [PK_QaQuestionTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_QaQuestionTranslations_question_lang] UNIQUE ([question_id], [language_prefix]),
                    CONSTRAINT [FK_QaQuestionTranslations_Questions]
                        FOREIGN KEY ([question_id]) REFERENCES [qa].[QaQuestions] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[qa].[QaAnswers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [qa].[QaAnswers]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_QaAnswers_id] DEFAULT (NEWSEQUENTIALID()),
                    [question_id] UNIQUEIDENTIFIER NOT NULL,
                    [ordered] INT NOT NULL CONSTRAINT [DF_QaAnswers_ordered] DEFAULT (1),
                    [publish] BIT NOT NULL CONSTRAINT [DF_QaAnswers_publish] DEFAULT (1),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NULL,
                    CONSTRAINT [PK_QaAnswers] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_QaAnswers_Questions]
                        FOREIGN KEY ([question_id]) REFERENCES [qa].[QaQuestions] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_QaAnswers_question_id]
                    ON [qa].[QaAnswers] ([question_id]);
                CREATE INDEX [IX_QaAnswers_question_id_ordered]
                    ON [qa].[QaAnswers] ([question_id], [ordered]);
                CREATE INDEX [IX_QaAnswers_publish]
                    ON [qa].[QaAnswers] ([publish]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[qa].[QaAnswerTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [qa].[QaAnswerTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_QaAnswerTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [answer_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(20) NOT NULL,
                    [answer_text] NVARCHAR(MAX) NOT NULL,
                    CONSTRAINT [PK_QaAnswerTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_QaAnswerTranslations_answer_lang] UNIQUE ([answer_id], [language_prefix]),
                    CONSTRAINT [FK_QaAnswerTranslations_Answers]
                        FOREIGN KEY ([answer_id]) REFERENCES [qa].[QaAnswers] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        logger.LogInformation("Qa schema ensured.");
    }
}
