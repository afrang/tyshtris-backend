using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Forms.Domain;

namespace TishtryaCMS.Modules.Forms.Infrastructure;

public sealed class FormsDbContext(DbContextOptions<FormsDbContext> options) : DbContext(options)
{
    public DbSet<FormDefinition> Forms => Set<FormDefinition>();
    public DbSet<FormTranslation> FormTranslations => Set<FormTranslation>();
    public DbSet<FormField> FormFields => Set<FormField>();
    public DbSet<FormFieldTranslation> FormFieldTranslations => Set<FormFieldTranslation>();
    public DbSet<FormSubmission> FormSubmissions => Set<FormSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("forms");

        modelBuilder.Entity<FormDefinition>(entity =>
        {
            entity.ToTable("Forms");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
            entity.Property(x => x.SubmitButtonText).HasColumnName("submit_button_text").HasMaxLength(120).IsRequired();
            entity.Property(x => x.SuccessMessage).HasColumnName("success_message").HasMaxLength(500);
            entity.Property(x => x.IsPublished).HasColumnName("is_published").IsRequired();
            entity.Property(x => x.CreatedBy).HasColumnName("created_by");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(x => x.Slug);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasIndex(x => x.IsPublished);

            entity.HasMany(x => x.Translations)
                .WithOne(x => x.Form)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Fields)
                .WithOne(x => x.Form)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Submissions)
                .WithOne(x => x.Form)
                .HasForeignKey(x => x.FormId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormTranslation>(entity =>
        {
            entity.ToTable("FormTranslations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.FormId).HasColumnName("form_id").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Slug).HasColumnName("slug").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("nvarchar(max)");
            entity.Property(x => x.SubmitButtonText).HasColumnName("submit_button_text").HasMaxLength(120).IsRequired();
            entity.Property(x => x.SuccessMessage).HasColumnName("success_message").HasMaxLength(500);

            entity.HasIndex(x => new { x.FormId, x.LanguagePrefix }).IsUnique();
            entity.HasIndex(x => new { x.LanguagePrefix, x.Slug }).IsUnique();
        });

        modelBuilder.Entity<FormField>(entity =>
        {
            entity.ToTable("FormFields");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.FormId).HasColumnName("form_id").IsRequired();
            entity.Property(x => x.FieldKey).HasColumnName("field_key").HasMaxLength(100).IsRequired();
            entity.Property(x => x.FieldType).HasColumnName("field_type").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Label).HasColumnName("label").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Placeholder).HasColumnName("placeholder").HasMaxLength(300);
            entity.Property(x => x.HelpText).HasColumnName("help_text").HasMaxLength(500);
            entity.Property(x => x.OptionsJson).HasColumnName("options_json").HasColumnType("nvarchar(max)");
            entity.Property(x => x.IsRequired).HasColumnName("is_required").IsRequired();
            entity.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(x => new { x.FormId, x.FieldKey }).IsUnique();
            entity.HasIndex(x => new { x.FormId, x.SortOrder });

            entity.HasMany(x => x.Translations)
                .WithOne(x => x.Field)
                .HasForeignKey(x => x.FieldId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormFieldTranslation>(entity =>
        {
            entity.ToTable("FormFieldTranslations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.FieldId).HasColumnName("field_id").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(16).IsRequired();
            entity.Property(x => x.Label).HasColumnName("label").HasMaxLength(300).IsRequired();
            entity.Property(x => x.Placeholder).HasColumnName("placeholder").HasMaxLength(300);
            entity.Property(x => x.HelpText).HasColumnName("help_text").HasMaxLength(500);

            entity.HasIndex(x => new { x.FieldId, x.LanguagePrefix }).IsUnique();
        });

        modelBuilder.Entity<FormSubmission>(entity =>
        {
            entity.ToTable("FormSubmissions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.FormId).HasColumnName("form_id").IsRequired();
            entity.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(16);
            entity.Property(x => x.IpAddress).HasColumnName("ip_address").HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasColumnName("user_agent").HasMaxLength(500);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            entity.HasIndex(x => x.FormId);
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}
