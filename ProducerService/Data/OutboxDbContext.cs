using Microsoft.EntityFrameworkCore;
using ProducerService.Models;

namespace ProducerService.Data;

public class OutboxDbContext : DbContext
{
  public OutboxDbContext(DbContextOptions<OutboxDbContext> options) : base(options)
  {
  }
  public DbSet<OutboxMessage> OutboxMessages { get; set; }
  public DbSet<TopicRegistration> TopicRegistrations { get; set; }
  public DbSet<ConsumerGroupRegistration> ConsumerGroupRegistrations { get; set; }
  public DbSet<ConsumerAcknowledgment> ConsumerAcknowledgments { get; set; }

  // Agent Management for Horizontal Scaling
  public DbSet<ProducerServiceAgent> ProducerServiceAgents { get; set; }
  public DbSet<ConsumerServiceAgent> ConsumerServiceAgents { get; set; }
  public DbSet<ServiceHealthCheck> ServiceHealthChecks { get; set; }

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
    base.OnModelCreating(modelBuilder);    // OutboxMessage configuration
    modelBuilder.Entity<OutboxMessage>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.Id).HasMaxLength(50);
      entity.Property(e => e.Topic).HasMaxLength(200).IsRequired();
      entity.Property(e => e.Message).IsRequired();
      entity.Property(e => e.ConsumerGroup).HasMaxLength(200).IsRequired();
      entity.Property(e => e.ErrorMessage).HasMaxLength(1000);
      entity.Property(e => e.Status).HasConversion<string>();
      entity.Property(e => e.TargetConsumerServiceId).HasMaxLength(100);
      entity.Property(e => e.OriginalMessageId).HasMaxLength(50);
      entity.Property(e => e.IdempotencyKey).HasMaxLength(100);

      entity.HasIndex(e => new { e.Status, e.CreatedAt });
      entity.HasIndex(e => new { e.Topic, e.ConsumerGroup });
      entity.HasIndex(e => e.CreatedAt);
      entity.HasIndex(e => e.IdempotencyKey);
      entity.HasIndex(e => new { e.IsRetry, e.ScheduledRetryAt });

      // Foreign key relationship
      entity.HasOne(e => e.TopicRegistration)
                .WithMany(t => t.Messages)
                .HasForeignKey(e => e.TopicRegistrationId)
                .OnDelete(DeleteBehavior.Restrict);
    });

    // TopicRegistration configuration
    modelBuilder.Entity<TopicRegistration>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.TopicName).HasMaxLength(200).IsRequired();
      entity.Property(e => e.Description).HasMaxLength(500);

      entity.HasIndex(e => e.TopicName).IsUnique();
    });

    // ConsumerGroupRegistration configuration
    modelBuilder.Entity<ConsumerGroupRegistration>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.ConsumerGroupName).HasMaxLength(200).IsRequired();

      entity.HasIndex(e => new { e.TopicRegistrationId, e.ConsumerGroupName }).IsUnique();

      // Foreign key relationship
      entity.HasOne(e => e.TopicRegistration)
                .WithMany(t => t.ConsumerGroups)
                .HasForeignKey(e => e.TopicRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
    });    // ConsumerAcknowledgment configuration
    modelBuilder.Entity<ConsumerAcknowledgment>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.MessageId).HasMaxLength(50).IsRequired();
      entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

      entity.HasIndex(e => new { e.MessageId, e.ConsumerGroupRegistrationId }).IsUnique();

      // Foreign key relationship
      entity.HasOne(e => e.ConsumerGroupRegistration)
                .WithMany(c => c.Acknowledgments)
                .HasForeignKey(e => e.ConsumerGroupRegistrationId)
                .OnDelete(DeleteBehavior.Cascade);
    });    // ProducerServiceAgent configuration
    modelBuilder.Entity<ProducerServiceAgent>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.InstanceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
      entity.Property(e => e.HostName).HasMaxLength(200);
      entity.Property(e => e.IpAddress).HasMaxLength(50);
      entity.Property(e => e.BaseUrl).HasMaxLength(500);
      entity.Property(e => e.Version).HasMaxLength(50);

      // Convert Dictionary to JSON
      entity.Property(e => e.Metadata)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

      entity.HasIndex(e => e.ServiceId).IsUnique();
      entity.HasIndex(e => e.InstanceId).IsUnique();
    });

    // ConsumerServiceAgent configuration
    modelBuilder.Entity<ConsumerServiceAgent>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.InstanceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.ServiceName).IsRequired().HasMaxLength(200);
      entity.Property(e => e.HostName).HasMaxLength(200);
      entity.Property(e => e.IpAddress).HasMaxLength(50);
      entity.Property(e => e.BaseUrl).HasMaxLength(500);
      entity.Property(e => e.Version).HasMaxLength(50);

      // Convert arrays to JSON
      entity.Property(e => e.AssignedConsumerGroups)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? Array.Empty<string>());

      entity.Property(e => e.AssignedTopics)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<string[]>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? Array.Empty<string>());

      // Convert Dictionary to JSON
      entity.Property(e => e.Metadata)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, string>());

      entity.HasIndex(e => e.ServiceId).IsUnique();
      entity.HasIndex(e => e.InstanceId).IsUnique();
    });

    // ServiceHealthCheck configuration
    modelBuilder.Entity<ServiceHealthCheck>(entity =>
    {
      entity.HasKey(e => e.Id);
      entity.Property(e => e.ServiceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.InstanceId).IsRequired().HasMaxLength(100);
      entity.Property(e => e.StatusMessage).HasMaxLength(1000);

      // Convert Dictionary to JSON
      entity.Property(e => e.HealthData)
        .HasConversion(
          v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
          v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new Dictionary<string, object>());

      entity.HasIndex(e => new { e.ServiceId, e.CheckedAt });
    });

    // Seed data
    SeedData(modelBuilder);
  }

  private void SeedData(ModelBuilder modelBuilder)
  {
    // Seed single shared topic registration
    modelBuilder.Entity<TopicRegistration>().HasData(
        new TopicRegistration
        {
          Id = 1,
          TopicName = "shared-events",
          Description = "Shared events topic for all message types in the outbox pattern demonstration",
          IsActive = true,
          CreatedAt = DateTime.UtcNow
        }
    );

    // Seed the 3 consumer groups for the shared-events topic
    modelBuilder.Entity<ConsumerGroupRegistration>().HasData(
        // Group A - High load processing (3 consumers: 5401, 5402, 5403)
        new ConsumerGroupRegistration
        {
          Id = 1,
          ConsumerGroupName = "group-a",
          TopicRegistrationId = 1,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 30,
          MaxRetries = -1, // Infinite retry for demonstration
          CreatedAt = DateTime.UtcNow
        },
        // Group B - Medium load processing (2 consumers: 5404, 5405)
        new ConsumerGroupRegistration
        {
          Id = 2,
          ConsumerGroupName = "group-b",
          TopicRegistrationId = 1,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 30,
          MaxRetries = -1, // Infinite retry for demonstration
          CreatedAt = DateTime.UtcNow
        },
        // Group C - Light load processing (1 consumer: 5406)
        new ConsumerGroupRegistration
        {
          Id = 3,
          ConsumerGroupName = "group-c",
          TopicRegistrationId = 1,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 30,
          MaxRetries = -1, // Infinite retry for demonstration
          CreatedAt = DateTime.UtcNow
        }
    );
  }
}
