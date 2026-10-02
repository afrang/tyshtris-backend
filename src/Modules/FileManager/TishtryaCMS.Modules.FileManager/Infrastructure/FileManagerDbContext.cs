using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.FileManager.Domain;

namespace TishtryaCMS.Modules.FileManager.Infrastructure;

public sealed class FileManagerDbContext(DbContextOptions<FileManagerDbContext> options) : DbContext(options)
{
    public DbSet<FileManagerEntry> FileManagers => Set<FileManagerEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("filemanager");

        modelBuilder.Entity<FileManagerEntry>(entity =>
        {
            entity.ToTable("FileManagers");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Component)
                .HasColumnName("component")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.ParentId)
                .HasColumnName("parent_id");

            entity.Property(x => x.Ordered)
                .HasColumnName("ordered")
                .HasDefaultValue(1)
                .IsRequired();

            entity.Property(x => x.Publish)
                .HasColumnName("publish")
                .HasDefaultValue(true)
                .IsRequired();

            entity.Property(x => x.Folder)
                .HasColumnName("folder")
                .HasMaxLength(500);

            entity.Property(x => x.Filename)
                .HasColumnName("filename")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Extension)
                .HasColumnName("extension")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.FullAddress)
                .HasColumnName("full_address")
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(x => x.Namefile)
                .HasColumnName("namefile")
                .HasMaxLength(255);

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime")
                .HasDefaultValueSql("GETDATE()")
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .HasColumnType("datetime");

            entity.HasIndex(x => new { x.Component, x.ParentId });
            entity.HasIndex(x => new { x.Component, x.ParentId, x.Ordered });
            entity.HasIndex(x => x.Filename);
            entity.HasIndex(x => x.Publish);
        });
    }
}
