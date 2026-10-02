using Microsoft.EntityFrameworkCore;
using TishtryaCMS.Modules.Tickets.Domain;

namespace TishtryaCMS.Modules.Tickets.Infrastructure;

public sealed class TicketsDbContext(DbContextOptions<TicketsDbContext> options) : DbContext(options)
{
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketReply> TicketReplies => Set<TicketReply>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("tickets");

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.ToTable("Tickets");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            entity.Property(x => x.AuthorName).HasColumnName("author_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.AuthorEmail).HasColumnName("author_email").HasMaxLength(256).IsRequired();
            entity.Property(x => x.Category).HasColumnName("category").HasMaxLength(40).IsRequired();
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasColumnName("body").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at").IsRequired();
            entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Category);
            entity.HasIndex(x => x.UpdatedAtUtc);

            entity.HasMany(x => x.Replies)
                .WithOne(x => x.Ticket)
                .HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketReply>(entity =>
        {
            entity.ToTable("TicketReplies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id").HasDefaultValueSql("NEWSEQUENTIALID()").ValueGeneratedOnAdd();
            entity.Property(x => x.TicketId).HasColumnName("ticket_id").IsRequired();
            entity.Property(x => x.AuthorUserId).HasColumnName("author_user_id").IsRequired();
            entity.Property(x => x.AuthorName).HasColumnName("author_name").HasMaxLength(200).IsRequired();
            entity.Property(x => x.FromStaff).HasColumnName("from_staff").IsRequired();
            entity.Property(x => x.Body).HasColumnName("body").HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at").IsRequired();

            entity.HasIndex(x => x.TicketId);
            entity.HasIndex(x => x.CreatedAtUtc);
        });
    }
}
