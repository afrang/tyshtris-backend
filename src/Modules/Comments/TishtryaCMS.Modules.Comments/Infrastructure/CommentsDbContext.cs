using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Comments.Domain;

namespace TishtryaCMS.Modules.Comments.Infrastructure;

public sealed class CommentsDbContext(DbContextOptions<CommentsDbContext> options) : DbContext(options)
{
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("comments");

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("Comments");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.Component).HasColumnName("component").HasMaxLength(100).IsRequired();
            entity.Property(x => x.ParentId).HasColumnName("parent_id").IsRequired();
            entity.Property(x => x.ParentCommentId).HasColumnName("parent_comment_id");
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.AuthorDisplayName).HasColumnName("author_display_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.AuthorEmail).HasColumnName("author_email").HasMaxLength(256);
            entity.Property(x => x.Body).HasColumnName("body").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(x => new { x.Component, x.ParentId });
            entity.HasIndex(x => new { x.Component, x.ParentId, x.Status });
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ParentCommentId);
            entity.HasIndex(x => x.CreatedAt);

            entity.HasOne(x => x.ParentComment)
                .WithMany(x => x.Replies)
                .HasForeignKey(x => x.ParentCommentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
