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
    // Seed default topic registrations
    modelBuilder.Entity<TopicRegistration>().HasData(
        new TopicRegistration
        {
          Id = 1,
          TopicName = "user-events",
          Description = "User management events",
          IsActive = true,
          CreatedAt = DateTime.UtcNow
        },
        new TopicRegistration
        {
          Id = 2,
          TopicName = "order-events",
          Description = "Order processing events",
          IsActive = true,
          CreatedAt = DateTime.UtcNow
        },
        new TopicRegistration
        {
          Id = 3,
          TopicName = "analytics-events",
          Description = "Analytics and tracking events",
          IsActive = true,
          CreatedAt = DateTime.UtcNow
        },
        new TopicRegistration
        {
          Id = 4,
          TopicName = "notification-events",
          Description = "Notification events",
          IsActive = true,
          CreatedAt = DateTime.UtcNow
        }
    );

    // Seed default consumer group registrations
    modelBuilder.Entity<ConsumerGroupRegistration>().HasData(
        // User events consumer groups
        new ConsumerGroupRegistration
        {
          Id = 1,
          ConsumerGroupName = "default-consumer-group",
          TopicRegistrationId = 1,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 30,
          MaxRetries = 3,
          CreatedAt = DateTime.UtcNow
        },
        // Order events consumer groups
        new ConsumerGroupRegistration
        {
          Id = 2,
          ConsumerGroupName = "default-consumer-group",
          TopicRegistrationId = 2,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 30,
          MaxRetries = 3,
          CreatedAt = DateTime.UtcNow
        },
        new ConsumerGroupRegistration
        {
          Id = 3,
          ConsumerGroupName = "inventory-service",
          TopicRegistrationId = 2,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 15,
          MaxRetries = 5,
          CreatedAt = DateTime.UtcNow
        },
        // Analytics events consumer groups
        new ConsumerGroupRegistration
        {
          Id = 4,
          ConsumerGroupName = "analytics-group",
          TopicRegistrationId = 3,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 60,
          MaxRetries = 2,
          CreatedAt = DateTime.UtcNow
        },
        // Notification events consumer groups
        new ConsumerGroupRegistration
        {
          Id = 5,
          ConsumerGroupName = "notification-group",
          TopicRegistrationId = 4,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 10,
          MaxRetries = 3,
          CreatedAt = DateTime.UtcNow
        },
        new ConsumerGroupRegistration
        {
          Id = 6,
          ConsumerGroupName = "email-service",
          TopicRegistrationId = 4,
          RequiresAcknowledgment = true,
          IsActive = true,
          AcknowledgmentTimeoutMinutes = 5,
          MaxRetries = 2,
          CreatedAt = DateTime.UtcNow
        }
    );
  }
}
