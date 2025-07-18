using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProducerService.Data;
using ProducerService.Jobs;
using ProducerService.Models;
using ProducerService.Services;
using Quartz;

Console.WriteLine("🚀 Starting Outbox Pattern Quartz.NET Test");

// Create host builder
var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

// SQLite connection for testing
var connectionString = "Data Source=outbox_test.db";

// Add DbContext with SQLite
builder.Services.AddDbContext<OutboxDbContext>(options =>
    options.UseSqlite(connectionString));

// Register services
builder.Services.AddScoped<IOutboxService, OutboxPostgreSqlService>();
builder.Services.AddScoped<IKafkaProducerService, MockKafkaProducerService>();
builder.Services.AddScoped<ITopicRegistrationService, TopicRegistrationService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddHttpClient();

// Configure Quartz.NET
builder.Services.AddQuartz(q =>
{
  q.UseMicrosoftDependencyInjection();

  // Configure jobs
  q.AddJob<ProcessPendingMessagesJob>(opts => opts.WithIdentity("ProcessPendingMessages"));
  q.AddJob<ProcessRetryMessagesJob>(opts => opts.WithIdentity("ProcessRetryMessages"));
  q.AddJob<AgentHeartbeatJob>(opts => opts.WithIdentity("AgentHeartbeat"));
  q.AddJob<CleanupOldMessagesJob>(opts => opts.WithIdentity("CleanupOldMessages"));

  // Configure triggers
  q.AddTrigger(opts => opts
      .ForJob("ProcessPendingMessages")
      .WithIdentity("ProcessPendingMessages-trigger")
      .WithSimpleSchedule(x => x
          .WithIntervalInSeconds(5)
          .RepeatForever()));

  q.AddTrigger(opts => opts
      .ForJob("ProcessRetryMessages")
      .WithIdentity("ProcessRetryMessages-trigger")
      .WithSimpleSchedule(x => x
          .WithIntervalInSeconds(30)
          .RepeatForever()));

  q.AddTrigger(opts => opts
      .ForJob("AgentHeartbeat")
      .WithIdentity("AgentHeartbeat-trigger")
      .WithSimpleSchedule(x => x
          .WithIntervalInSeconds(10)
          .RepeatForever()));

  q.AddTrigger(opts => opts
      .ForJob("CleanupOldMessages")
      .WithIdentity("CleanupOldMessages-trigger")
      .WithSimpleSchedule(x => x
          .WithIntervalInMinutes(60)
          .RepeatForever()));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Build and run
var host = builder.Build();

// Create database and run migrations
using (var scope = host.Services.CreateScope())
{
  var context = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
  await context.Database.EnsureCreatedAsync();
  Console.WriteLine("✅ Database created successfully");
}

Console.WriteLine("🎯 Starting Quartz.NET jobs...");
await host.RunAsync();

// Mock Kafka Producer for testing
public class MockKafkaProducerService : IKafkaProducerService
{
  private readonly ILogger<MockKafkaProducerService> _logger;

  public MockKafkaProducerService(ILogger<MockKafkaProducerService> logger)
  {
    _logger = logger;
  }

  public async Task<bool> SendMessageAsync(string topic, string key, string value, Dictionary<string, string>? headers = null)
  {
    _logger.LogInformation("📤 Mock Kafka: Sending message to topic {Topic} with key {Key}", topic, key);

    // Simulate processing delay
    await Task.Delay(100);

    // Simulate occasional failures for testing retry logic
    var random = new Random();
    if (random.Next(1, 10) <= 8) // 80% success rate
    {
      _logger.LogInformation("✅ Mock Kafka: Message sent successfully");
      return true;
    }
    else
    {
      _logger.LogWarning("❌ Mock Kafka: Message sending failed (simulated failure)");
      return false;
    }
  }
}
