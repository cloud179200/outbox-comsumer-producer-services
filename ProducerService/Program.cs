using Microsoft.EntityFrameworkCore;
using ProducerService.Services;
using ProducerService.Data;
using ProducerService.Models;
using ProducerService.Jobs;
using System.Net;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Get Service ID from environment variable
var serviceId = Environment.GetEnvironmentVariable("SERVICE_ID")
    ?? Environment.GetEnvironmentVariable("PRODUCER_SERVICE_ID")
    ?? $"producer-{Environment.MachineName}";

var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
    ?? $"{serviceId}-{Guid.NewGuid():N}";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// Database configuration - PostgreSQL or SQLite for testing
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password";

builder.Services.AddDbContext<OutboxDbContext>(options =>
{
    if (connectionString.StartsWith("Data Source="))
    {
        // SQLite configuration for testing
        options.UseSqlite(connectionString);
    }
    else
    {
        // PostgreSQL configuration for production
        options.UseNpgsql(connectionString);
    }
});

// Register services
builder.Services.AddScoped<IOutboxService, OutboxPostgreSqlService>();
builder.Services.AddScoped<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddScoped<ITopicRegistrationService, TopicRegistrationService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddHttpClient();

// Configure Quartz.NET
builder.Services.AddQuartz(q =>
{
    // Process pending messages job
    var processPendingJobKey = new JobKey("ProcessPendingMessages");
    q.AddJob<ProcessPendingMessagesJob>(opts => opts.WithIdentity(processPendingJobKey));
    q.AddTrigger(opts => opts
        .ForJob(processPendingJobKey)
        .WithIdentity("ProcessPendingMessages-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(5)
            .RepeatForever()));

    // Process retry messages job
    var processRetryJobKey = new JobKey("ProcessRetryMessages");
    q.AddJob<ProcessRetryMessagesJob>(opts => opts.WithIdentity(processRetryJobKey));
    q.AddTrigger(opts => opts
        .ForJob(processRetryJobKey)
        .WithIdentity("ProcessRetryMessages-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(10)
            .RepeatForever()));

    // Agent heartbeat job
    var heartbeatJobKey = new JobKey("AgentHeartbeat");
    q.AddJob<AgentHeartbeatJob>(opts => opts.WithIdentity(heartbeatJobKey));
    q.AddTrigger(opts => opts
        .ForJob(heartbeatJobKey)
        .WithIdentity("AgentHeartbeat-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(30)
            .RepeatForever()));

    // Cleanup old messages job
    var cleanupJobKey = new JobKey("CleanupOldMessages");
    q.AddJob<CleanupOldMessagesJob>(opts => opts.WithIdentity(cleanupJobKey));
    q.AddTrigger(opts => opts
        .ForJob(cleanupJobKey)
        .WithIdentity("CleanupOldMessages-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInHours(1)
            .RepeatForever()));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Logging
builder.Logging.AddConsole();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<OutboxDbContext>();
    await context.Database.EnsureCreatedAsync();

    // Register this producer service instance
    var agentService = scope.ServiceProvider.GetRequiredService<IAgentService>();
    var hostName = Environment.MachineName;
    var ipAddress = Environment.GetEnvironmentVariable("ASPNETCORE_URLS")?.Split("://").LastOrDefault()?.Split(":").FirstOrDefault() ?? "localhost";

    // For Docker container networking, use the container name as hostname
    if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" &&
        Environment.GetEnvironmentVariable("SERVICE_ID") != null)
    {
        hostName = Environment.GetEnvironmentVariable("SERVICE_ID") ?? hostName;
    }

    var port = 80; // Default port for containerized services

    var registration = new AgentRegistrationRequest
    {
        ServiceId = serviceId,
        ServiceName = "Producer Service",
        HostName = hostName,
        IpAddress = ipAddress,
        Port = port,
        BaseUrl = $"http://{ipAddress}:{port}",
        ServiceType = ServiceType.Producer,
        Version = "1.0.0",
        Metadata = new Dictionary<string, string>
        {
            ["Environment"] = app.Environment.EnvironmentName,
            ["StartTime"] = DateTime.UtcNow.ToString("O"),
            ["MachineName"] = Environment.MachineName,
            ["ProcessId"] = Environment.ProcessId.ToString()
        }
    };

    await agentService.RegisterProducerAgentAsync(registration);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
