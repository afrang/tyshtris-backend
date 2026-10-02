using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TishtryaCMS.Modules.Forms.Domain;

namespace TishtryaCMS.Modules.Forms.Infrastructure;

public static class FormsSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FormsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("FormsSeeder");

        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'forms')
                EXEC(N'CREATE SCHEMA [forms]');
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[forms].[Forms]', N'U') IS NULL
            BEGIN
                CREATE TABLE [forms].[Forms]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Forms_id] DEFAULT (NEWSEQUENTIALID()),
                    [title] NVARCHAR(300) NOT NULL,
                    [slug] NVARCHAR(300) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [submit_button_text] NVARCHAR(120) NOT NULL CONSTRAINT [DF_Forms_submit] DEFAULT (N'Submit'),
                    [success_message] NVARCHAR(500) NULL,
                    [is_published] BIT NOT NULL CONSTRAINT [DF_Forms_published] DEFAULT (0),
                    [created_by] UNIQUEIDENTIFIER NULL,
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_Forms] PRIMARY KEY ([id])
                );

                CREATE INDEX [IX_Forms_slug] ON [forms].[Forms] ([slug]);
                CREATE INDEX [IX_Forms_created_at] ON [forms].[Forms] ([created_at]);
                CREATE INDEX [IX_Forms_is_published] ON [forms].[Forms] ([is_published]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[forms].[FormTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [forms].[FormTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FormTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [form_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(16) NOT NULL,
                    [title] NVARCHAR(300) NOT NULL,
                    [slug] NVARCHAR(300) NOT NULL,
                    [description] NVARCHAR(MAX) NULL,
                    [submit_button_text] NVARCHAR(120) NOT NULL,
                    [success_message] NVARCHAR(500) NULL,
                    CONSTRAINT [PK_FormTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_FormTranslations_form_lang] UNIQUE ([form_id], [language_prefix]),
                    CONSTRAINT [UQ_FormTranslations_lang_slug] UNIQUE ([language_prefix], [slug]),
                    CONSTRAINT [FK_FormTranslations_Forms_form_id]
                        FOREIGN KEY ([form_id]) REFERENCES [forms].[Forms] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[forms].[FormFields]', N'U') IS NULL
            BEGIN
                CREATE TABLE [forms].[FormFields]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FormFields_id] DEFAULT (NEWSEQUENTIALID()),
                    [form_id] UNIQUEIDENTIFIER NOT NULL,
                    [field_key] NVARCHAR(100) NOT NULL,
                    [field_type] NVARCHAR(40) NOT NULL,
                    [label] NVARCHAR(300) NOT NULL,
                    [placeholder] NVARCHAR(300) NULL,
                    [help_text] NVARCHAR(500) NULL,
                    [options_json] NVARCHAR(MAX) NULL,
                    [is_required] BIT NOT NULL CONSTRAINT [DF_FormFields_required] DEFAULT (0),
                    [sort_order] INT NOT NULL CONSTRAINT [DF_FormFields_sort] DEFAULT (0),
                    [created_at] DATETIME2 NOT NULL,
                    [updated_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_FormFields] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_FormFields_form_key] UNIQUE ([form_id], [field_key]),
                    CONSTRAINT [FK_FormFields_Forms_form_id]
                        FOREIGN KEY ([form_id]) REFERENCES [forms].[Forms] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_FormFields_form_sort] ON [forms].[FormFields] ([form_id], [sort_order]);
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[forms].[FormFieldTranslations]', N'U') IS NULL
            BEGIN
                CREATE TABLE [forms].[FormFieldTranslations]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FormFieldTranslations_id] DEFAULT (NEWSEQUENTIALID()),
                    [field_id] UNIQUEIDENTIFIER NOT NULL,
                    [language_prefix] NVARCHAR(16) NOT NULL,
                    [label] NVARCHAR(300) NOT NULL,
                    [placeholder] NVARCHAR(300) NULL,
                    [help_text] NVARCHAR(500) NULL,
                    CONSTRAINT [PK_FormFieldTranslations] PRIMARY KEY ([id]),
                    CONSTRAINT [UQ_FormFieldTranslations_field_lang] UNIQUE ([field_id], [language_prefix]),
                    CONSTRAINT [FK_FormFieldTranslations_FormFields_field_id]
                        FOREIGN KEY ([field_id]) REFERENCES [forms].[FormFields] ([id]) ON DELETE CASCADE
                );
            END
            """, cancellationToken);

        await db.Database.ExecuteSqlRawAsync("""
            IF OBJECT_ID(N'[forms].[FormSubmissions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [forms].[FormSubmissions]
                (
                    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_FormSubmissions_id] DEFAULT (NEWSEQUENTIALID()),
                    [form_id] UNIQUEIDENTIFIER NOT NULL,
                    [payload_json] NVARCHAR(MAX) NOT NULL,
                    [language_prefix] NVARCHAR(16) NULL,
                    [ip_address] NVARCHAR(64) NULL,
                    [user_agent] NVARCHAR(500) NULL,
                    [created_at] DATETIME2 NOT NULL,
                    CONSTRAINT [PK_FormSubmissions] PRIMARY KEY ([id]),
                    CONSTRAINT [FK_FormSubmissions_Forms_form_id]
                        FOREIGN KEY ([form_id]) REFERENCES [forms].[Forms] ([id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_FormSubmissions_form_id] ON [forms].[FormSubmissions] ([form_id]);
                CREATE INDEX [IX_FormSubmissions_created_at] ON [forms].[FormSubmissions] ([created_at]);
            END
            """, cancellationToken);

        await EnsureContactFormAsync(db, cancellationToken);

        logger.LogInformation("Forms schema ensured.");
    }

    private static async Task EnsureContactFormAsync(FormsDbContext db, CancellationToken cancellationToken)
    {
        var exists = await db.Forms.AsNoTracking().AnyAsync(x => x.Slug == "contactus", cancellationToken);
        if (exists)
        {
            return;
        }

        var form = FormDefinition.Create(
            "Contact us",
            "contactus",
            "Messages sent from the public contact page.",
            "Send message",
            "Message sent.",
            isPublished: true,
            createdBy: null);

        db.Forms.Add(form);
        await db.SaveChangesAsync(cancellationToken);

        db.FormTranslations.Add(FormTranslation.Create(
            form.Id, "en", "Contact us", "contactus",
            "Messages sent from the public contact page.", "Send message", "Message sent."));
        db.FormTranslations.Add(FormTranslation.Create(
            form.Id, "fa", "تماس با ما", "contactus",
            "پیام‌های صفحه تماس با ما.", "ارسال پیام", "پیام ارسال شد."));
        db.FormTranslations.Add(FormTranslation.Create(
            form.Id, "ar", "اتصل بنا", "contactus",
            "الرسائل المرسلة من صفحة الاتصال.", "إرسال الرسالة", "أُرسلت الرسالة."));

        var fields = new (string Key, string Type, string Label, bool Required, int Order)[]
        {
            ("name", FormFieldTypes.Text, "Name", true, 0),
            ("email", FormFieldTypes.Email, "Email", true, 1),
            ("phone", FormFieldTypes.Tel, "Phone", false, 2),
            ("subject", FormFieldTypes.Text, "Subject", true, 3),
            ("message", FormFieldTypes.Textarea, "Message", true, 4),
        };

        foreach (var item in fields)
        {
            var field = FormField.Create(
                form.Id,
                item.Key,
                item.Type,
                item.Label,
                placeholder: null,
                helpText: null,
                optionsJson: null,
                item.Required,
                item.Order);
            db.FormFields.Add(field);
            await db.SaveChangesAsync(cancellationToken);
            db.FormFieldTranslations.Add(FormFieldTranslation.Create(
                field.Id, "en", item.Label, null, null));
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
