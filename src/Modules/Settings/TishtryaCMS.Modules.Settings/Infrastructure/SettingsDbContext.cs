using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Settings.Domain;

namespace TishtryaCMS.Modules.Settings.Infrastructure;

public sealed class SettingsDbContext(DbContextOptions<SettingsDbContext> options) : DbContext(options)
{
    public DbSet<SiteSettings> SiteSettings => Set<SiteSettings>();
    public DbSet<SiteSettingsTranslation> SiteSettingsTranslations => Set<SiteSettingsTranslation>();
    public DbSet<Language> Languages => Set<Language>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("settings");

        modelBuilder.Entity<Language>(entity =>
        {
            entity.ToTable("Languages");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(x => x.Prefix)
                .HasColumnName("prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.IsDefault)
                .HasColumnName("is_default")
                .IsRequired();

            entity.Property(x => x.Direction)
                .HasColumnName("direction")
                .HasMaxLength(10)
                .IsRequired();

            entity.HasIndex(x => x.Prefix).IsUnique();
        });

        modelBuilder.Entity<SiteSettings>(entity =>
        {
            entity.ToTable("SiteSettings");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id");

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.LogoUrl)
                .HasColumnName("logo_url")
                .HasMaxLength(1000);

            entity.Property(x => x.SocialLinksJson)
                .HasColumnName("social_links_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.ContactAddressesJson)
                .HasColumnName("contact_addresses_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.ContactPhonesJson)
                .HasColumnName("contact_phones_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.ContactEmailsJson)
                .HasColumnName("contact_emails_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.Property(x => x.StorageProvider)
                .HasColumnName("storage_provider")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.S3Endpoint)
                .HasColumnName("s3_endpoint")
                .HasMaxLength(500);

            entity.Property(x => x.S3Bucket)
                .HasColumnName("s3_bucket")
                .HasMaxLength(200);

            entity.Property(x => x.S3AccessKey)
                .HasColumnName("s3_access_key")
                .HasMaxLength(500);

            entity.Property(x => x.S3SecretKey)
                .HasColumnName("s3_secret_key")
                .HasMaxLength(500);

            entity.Property(x => x.S3Region)
                .HasColumnName("s3_region")
                .HasMaxLength(100);

            entity.Property(x => x.S3PublicBaseUrl)
                .HasColumnName("s3_public_base_url")
                .HasMaxLength(500);

            entity.Property(x => x.UpdatedAtUtc)
                .HasColumnName("updated_at_utc")
                .IsRequired();
        });

        modelBuilder.Entity<SiteSettingsTranslation>(entity =>
        {
            entity.ToTable("SiteSettingsTranslations");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id)
                .HasColumnName("id")
                .HasDefaultValueSql("NEWSEQUENTIALID()")
                .ValueGeneratedOnAdd();

            entity.Property(x => x.SiteSettingsId)
                .HasColumnName("site_settings_id")
                .IsRequired();

            entity.Property(x => x.LanguagePrefix)
                .HasColumnName("language_prefix")
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(x => x.Name)
                .HasColumnName("name")
                .HasMaxLength(200)
                .IsRequired();

            entity.Property(x => x.Title)
                .HasColumnName("title")
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(x => x.Keyword)
                .HasColumnName("keyword")
                .HasMaxLength(500);

            entity.Property(x => x.Description)
                .HasColumnName("description")
                .HasColumnType("nvarchar(max)");

            entity.Property(x => x.ContactAddressesJson)
                .HasColumnName("contact_addresses_json")
                .HasColumnType("nvarchar(max)")
                .IsRequired();

            entity.HasIndex(x => new { x.SiteSettingsId, x.LanguagePrefix }).IsUnique();

            entity.HasOne(x => x.SiteSettings)
                .WithMany(x => x.Translations)
                .HasForeignKey(x => x.SiteSettingsId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
