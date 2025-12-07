using Microsoft.EntityFrameworkCore;
using SlimTrack.Models;

namespace SlimTrack.Data.Database;

public class DbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public DbContext(DbContextOptions<DbContext> options)
        :base(options)
    {
    }

    public DbSet<Order> Orders{ get; set; }
    public DbSet<OrderEvent> OrderEvents => Set<OrderEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Description)
                .HasMaxLength(500);

            entity.Property(e => e.CurrentStatus)
                .IsRequired()
                .HasConversion<int>();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            entity.HasMany(o => o.Events)
                .WithOne()
                .HasForeignKey(e => e.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.OrderId)
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

            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Timestamp);
        });

    }
}

