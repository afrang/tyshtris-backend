using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.ContentModules.Domain;

namespace TishtryaCMS.Modules.ContentModules.Infrastructure;

public sealed class ContentModulesDbContext(DbContextOptions<ContentModulesDbContext> options) : DbContext(options)
{
    public DbSet<BlogGroup> BlogGroups => Set<BlogGroup>();
    public DbSet<BlogPost> BlogPosts => Set<BlogPost>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Gallery> Galleries => Set<Gallery>();
    public DbSet<BlogPostGroup> BlogPostGroups => Set<BlogPostGroup>();
    public DbSet<BlogPostTag> BlogPostTags => Set<BlogPostTag>();
    public DbSet<MenuGroup> MenuGroups => Set<MenuGroup>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<BlogPostTranslation> BlogPostTranslations => Set<BlogPostTranslation>();
    public DbSet<BlogGroupTranslation> BlogGroupTranslations => Set<BlogGroupTranslation>();
    public DbSet<TagTranslation> TagTranslations => Set<TagTranslation>();
    public DbSet<GalleryTranslation> GalleryTranslations => Set<GalleryTranslation>();
    public DbSet<MenuGroupTranslation> MenuGroupTranslations => Set<MenuGroupTranslation>();
    public DbSet<MenuItemTranslation> MenuItemTranslations => Set<MenuItemTranslation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("content");

        modelBuilder.Entity<BlogGroup>(entity =>
        {
            entity.ToTable("BlogGroups");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(x => x.Slug);

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.ParentId)
                .HasColumnName("parent_id");

            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.ParentId);
        });

        modelBuilder.Entity<BlogPost>(entity =>
        {
            entity.ToTable("BlogPosts");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(x => x.Slug);

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Content)
                .HasColumnName("content")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.MetaTitle)
                .HasColumnName("meta_title")
                .HasMaxLength(255);

            entity.Property(x => x.MetaDescription)
                .HasColumnName("meta_description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.CommentsEnabled)
                .HasColumnName("comments_enabled")
                .HasDefaultValue(false)
                .IsRequired();

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.Slug);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<BlogPostGroup>(entity =>
        {
            entity.ToTable("BlogPostGroups");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.PostId)
                .HasColumnName("post_id")
                .IsRequired();

            entity.Property(x => x.GroupId)
                .HasColumnName("group_id")
                .IsRequired();

            entity.HasIndex(x => x.PostId);
            entity.HasIndex(x => x.GroupId);
            entity.HasIndex(x => new { x.PostId, x.GroupId }).IsUnique();

            entity.HasOne(x => x.Post)
                .WithMany(x => x.PostGroups)
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Group)
                .WithMany()
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BlogPostTag>(entity =>
        {
            entity.ToTable("BlogPostTags");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.PostId)
                .HasColumnName("post_id")
                .IsRequired();

            entity.Property(x => x.TagId)
                .HasColumnName("tag_id")
                .IsRequired();

            entity.HasIndex(x => x.PostId);
            entity.HasIndex(x => x.TagId);
            entity.HasIndex(x => new { x.PostId, x.TagId }).IsUnique();

            entity.HasOne(x => x.Post)
                .WithMany(x => x.PostTags)
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Tag)
                .WithMany(x => x.PostTags)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Gallery>(entity =>
        {
            entity.ToTable("Galleries");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(x => x.Slug);

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.CreatedBy)
                .HasColumnName("created_by");

            entity.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();

            entity.HasIndex(x => x.CreatedBy);
        });

        modelBuilder.Entity<MenuGroup>(entity =>
        {
            entity.ToTable("MenuGroups");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Key)
                .HasColumnName("key")
                .HasMaxLength(100)
                .IsRequired();

            entity.HasIndex(x => x.Key).IsUnique();

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            entity.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .IsRequired();
        });

        modelBuilder.Entity<MenuItem>(entity =>
        {
            entity.ToTable("MenuItems");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Url)
                .HasColumnName("url")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.ParentId)
                .HasColumnName("parent_id");

            entity.Property(x => x.GroupId)
                .HasColumnName("group_id")
                .IsRequired();

            entity.Property(x => x.Data)
                .HasColumnName("data")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Function)
                .HasColumnName("function")
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(x => x.TargetId)
                .HasColumnName("target_id");

            entity.Property(x => x.IsMegaMenu)
                .HasColumnName("is_mega_menu")
                .IsRequired();

            entity.Property(x => x.SortOrder)
                .HasColumnName("sort_order")
                .IsRequired();

            entity.Property(x => x.IsActive)
                .HasColumnName("is_active")
                .IsRequired();

            entity.HasOne(x => x.Group)
                .WithMany(x => x.Items)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => x.GroupId);
            entity.HasIndex(x => x.ParentId);
            entity.HasIndex(x => x.TargetId);
        });

        modelBuilder.Entity<BlogPostTranslation>(entity =>
        {
            entity.ToTable("BlogPostTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.PostId)
                .HasColumnName("post_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.Content)
                .HasColumnName("content")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.MetaTitle)
                .HasColumnName("meta_title")
                .HasMaxLength(255);

            entity.Property(x => x.MetaDescription)
                .HasColumnName("meta_description")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.PostId, x.LanguagePrefix }).IsUnique();
            entity.HasIndex(x => new { x.LanguagePrefix, x.Slug }).IsUnique();

            entity.HasOne(x => x.Post)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.PostId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BlogGroupTranslation>(entity =>
        {
            entity.ToTable("BlogGroupTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.GroupId)
                .HasColumnName("group_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.GroupId, x.LanguagePrefix }).IsUnique();
            entity.HasIndex(x => new { x.LanguagePrefix, x.Slug }).IsUnique();

            entity.HasOne(x => x.Group)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TagTranslation>(entity =>
        {
            entity.ToTable("TagTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.TagId)
                .HasColumnName("tag_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.TagId, x.LanguagePrefix }).IsUnique();
            entity.HasIndex(x => new { x.LanguagePrefix, x.Slug }).IsUnique();

            entity.HasOne(x => x.Tag)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GalleryTranslation>(entity =>
        {
            entity.ToTable("GalleryTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.GalleryId)
                .HasColumnName("gallery_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Slug)
                .HasColumnName("slug")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.GalleryId, x.LanguagePrefix }).IsUnique();
            entity.HasIndex(x => new { x.LanguagePrefix, x.Slug }).IsUnique();

            entity.HasOne(x => x.Gallery)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.GalleryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuGroupTranslation>(entity =>
        {
            entity.ToTable("MenuGroupTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.GroupId)
                .HasColumnName("group_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.GroupId, x.LanguagePrefix }).IsUnique();

            entity.HasOne(x => x.Group)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuItemTranslation>(entity =>
        {
            entity.ToTable("MenuItemTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.ItemId)
                .HasColumnName("item_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Url)
                .HasColumnName("url")
                .HasMaxLength(500)
                .IsRequired();

            entity.Property(x => x.Data)
                .HasColumnName("data")
                .HasColumnType("nvarchar(max)");

            entity.HasIndex(x => new { x.ItemId, x.LanguagePrefix }).IsUnique();

            entity.HasOne(x => x.Item)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
