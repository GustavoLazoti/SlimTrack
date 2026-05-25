using Microsoft.EntityFrameworkCore;
using SlimTrack.Models;

namespace SlimTrack.Data.Database;

public class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<ClassificationLog> ClassificationLogs => Set<ClassificationLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.PasswordHash)
                .IsRequired();

            entity.Property(e => e.Role)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasIndex(e => e.Email)
                .IsUnique();
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.RequesterName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.RequesterEmail)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Category)
                .HasConversion<int?>();

            entity.Property(e => e.Priority)
                .HasConversion<int?>();

            entity.Property(e => e.ClassificationRationale)
                .HasMaxLength(1000);

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            entity.HasOne(t => t.CreatedByUser)
                .WithMany(u => u.Tickets)
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(t => t.Events)
                .WithOne()
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasIndex(e => e.RequesterEmail);
        });

        modelBuilder.Entity<TicketEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TicketId)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Message)
                .HasMaxLength(1000);

            entity.Property(e => e.Metadata)
                .HasMaxLength(2000);

            entity.Property(e => e.Timestamp)
                .IsRequired();

            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<ClassificationLog>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TicketId)
                .IsRequired();

            entity.Property(e => e.InputTitle)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.InputDescription)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(e => e.SuggestedCategory)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.SuggestedPriority)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.Rationale)
                .IsRequired()
                .HasMaxLength(1000);

            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Payload)
                .IsRequired();

            entity.Property(e => e.Published)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.RetryCount)
                .IsRequired();

            entity.Property(e => e.ErrorMessage)
                .HasMaxLength(2000);

            entity.HasIndex(e => new { e.Published, e.CreatedAt });
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}

