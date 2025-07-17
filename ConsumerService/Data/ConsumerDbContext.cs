using Microsoft.EntityFrameworkCore;
using ConsumerService.Models;

namespace ConsumerService.Data;

public class ConsumerDbContext : DbContext
{
  public ConsumerDbContext(DbContextOptions<ConsumerDbContext> options) : base(options)
  {
  }

  public DbSet<ProcessedMessage> ProcessedMessages { get; set; }
  public DbSet<FailedMessage> FailedMessages { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);

    // ProcessedMessage configuration
    modelBuilder.Entity<ProcessedMessage>(entity =>
    {
      entity.HasKey(e => new { e.MessageId, e.ConsumerGroup });
      entity.Property(e => e.MessageId).HasMaxLength(50);
      entity.Property(e => e.ConsumerGroup).HasMaxLength(200);
      entity.Property(e => e.Topic).HasMaxLength(200);

      entity.HasIndex(e => new { e.ConsumerGroup, e.ProcessedAt });
      entity.HasIndex(e => e.ProcessedAt);
    });

    // FailedMessage configuration
    modelBuilder.Entity<FailedMessage>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.MessageId).HasMaxLength(50).IsRequired();
      entity.Property(e => e.ConsumerGroup).HasMaxLength(200).IsRequired();
      entity.Property(e => e.Topic).HasMaxLength(200);
      entity.Property(e => e.ErrorMessage).HasMaxLength(2000);

      entity.HasIndex(e => new { e.MessageId, e.ConsumerGroup });
      entity.HasIndex(e => e.FailedAt);
    });
  }
}
