using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Qa.Domain;

namespace TishtryaCMS.Modules.Qa.Infrastructure;

public sealed class QaDbContext(DbContextOptions<QaDbContext> options) : DbContext(options)
{
    public DbSet<QaQuestion> Questions => Set<QaQuestion>();
    public DbSet<QaQuestionTranslation> QuestionTranslations => Set<QaQuestionTranslation>();
    public DbSet<QaAnswer> Answers => Set<QaAnswer>();
    public DbSet<QaAnswerTranslation> AnswerTranslations => Set<QaAnswerTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("qa");

        modelBuilder.Entity<QaQuestion>(entity =>
        {
            entity.ToTable("QaQuestions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.Component).HasColumnName("component").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id").IsRequired();
            entity.Property(x => x.Ordered).HasColumnName("ordered").HasDefaultValue(1).IsRequired();
            entity.Property(x => x.Publish).HasColumnName("publish").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => new { x.Component, x.ParentId });
            entity.HasIndex(x => new { x.Component, x.ParentId, x.Ordered });
            entity.HasIndex(x => x.Publish);
        });

        modelBuilder.Entity<QaQuestionTranslation>(entity =>
        {
            entity.ToTable("QaQuestionTranslations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(20).IsRequired();
            entity.Property(x => x.QuestionText).HasColumnName("question_text").HasMaxLength(2000).IsRequired();

            entity.HasIndex(x => new { x.QuestionId, x.LanguagePrefix }).IsUnique();

            entity.HasOne(x => x.Question)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QaAnswer>(entity =>
        {
            entity.ToTable("QaAnswers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.QuestionId).HasColumnName("question_id").IsRequired();
            entity.Property(x => x.Ordered).HasColumnName("ordered").HasDefaultValue(1).IsRequired();
            entity.Property(x => x.Publish).HasColumnName("publish").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.QuestionId);
            entity.HasIndex(x => new { x.QuestionId, x.Ordered });
            entity.HasIndex(x => x.Publish);

            entity.HasOne(x => x.Question)
                .WithMany(x => x.Answers)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QaAnswerTranslation>(entity =>
        {
            entity.ToTable("QaAnswerTranslations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.AnswerId).HasColumnName("answer_id").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(20).IsRequired();
            entity.Property(x => x.AnswerText).HasColumnName("answer_text").HasColumnType("nvarchar(max)").IsRequired();

            entity.HasIndex(x => new { x.AnswerId, x.LanguagePrefix }).IsUnique();

            entity.HasOne(x => x.Answer)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.AnswerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
