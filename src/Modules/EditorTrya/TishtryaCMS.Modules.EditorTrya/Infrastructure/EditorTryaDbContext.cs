using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.EditorTrya.Domain;

namespace TishtryaCMS.Modules.EditorTrya.Infrastructure;

public sealed class EditorTryaDbContext(DbContextOptions<EditorTryaDbContext> options) : DbContext(options)
{
    public DbSet<EditorTryaContent> Contents => Set<EditorTryaContent>();
    public DbSet<EditorTryaContainer> Containers => Set<EditorTryaContainer>();
    public DbSet<EditorTryaComponent> Components => Set<EditorTryaComponent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("editortrya");

        modelBuilder.Entity<EditorTryaContent>(entity =>
        {
            entity.ToTable("EditorTryaContents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.Component).HasColumnName("component").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id").IsRequired();
            entity.Property(x => x.LanguagePrefix).HasColumnName("language_prefix").HasMaxLength(20).IsRequired();
            entity.Property(x => x.Publish).HasColumnName("publish").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.HasIndex(x => new { x.Component, x.ParentId, x.LanguagePrefix }).IsUnique();
        });

        modelBuilder.Entity<EditorTryaContainer>(entity =>
        {
            entity.ToTable("EditorTryaContainers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id");
            entity.Property(x => x.Component).HasColumnName("component").HasMaxLength(100);
            entity.Property(x => x.Cols).HasColumnName("cols");
            entity.Property(x => x.Ordered).HasColumnName("ordered").HasDefaultValue(1).IsRequired();
            entity.Property(x => x.Publish).HasColumnName("publish").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.Options).HasColumnName("options").HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.ContentId);
            entity.HasIndex(x => new { x.ContentId, x.Ordered });
            entity.HasIndex(x => x.ParentId);

            entity.HasOne(x => x.Content)
                .WithMany(x => x.Containers)
                .HasForeignKey(x => x.ContentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EditorTryaComponent>(entity =>
        {
            entity.ToTable("EditorTryaComponents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.ContainerId).HasColumnName("container_id").IsRequired();
            entity.Property(x => x.Type).HasColumnName("type").HasMaxLength(100).IsRequired();
            entity.Property(x => x.Ordered).HasColumnName("ordered").HasDefaultValue(1).IsRequired();
            entity.Property(x => x.Publish).HasColumnName("publish").HasDefaultValue(true).IsRequired();
            entity.Property(x => x.Data).HasColumnName("data").HasColumnType("nvarchar(max)");
            entity.Property(x => x.Options).HasColumnName("options").HasColumnType("nvarchar(max)");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasIndex(x => x.ContainerId);
            entity.HasIndex(x => new { x.ContainerId, x.Ordered });
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.Publish);

            entity.HasOne(x => x.Container)
                .WithMany(x => x.Components)
                .HasForeignKey(x => x.ContainerId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
