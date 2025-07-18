using Microsoft.EntityFrameworkCore;
using ConsumerService.Services;
using ConsumerService.Data;
using ConsumerService.Models;
using ConsumerService.Jobs;
using System.Net;
using Quartz;

var builder = WebApplication.CreateBuilder(args);

// Get Service ID from environment variable
var serviceId = Environment.GetEnvironmentVariable("SERVICE_ID")
    ?? Environment.GetEnvironmentVariable("CONSUMER_SERVICE_ID")
    ?? $"consumer-{Environment.MachineName}";

var instanceId = Environment.GetEnvironmentVariable("INSTANCE_ID")
    ?? $"{serviceId}-{Guid.NewGuid():N}";

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// Database configuration - PostgreSQL or SQLite for testing
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password";

builder.Services.AddDbContext<ConsumerDbContext>(options =>
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
builder.Services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();
builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();
builder.Services.AddScoped<IConsumerTrackingService, ConsumerPostgreSqlTrackingService>();
builder.Services.AddHttpClient();

// Get consumer group configuration from appsettings
var consumerGroups = builder.Configuration.GetSection("ConsumerGroups").Get<ConsumerGroupConfig[]>()
    ?? new ConsumerGroupConfig[]
    {
        new ConsumerGroupConfig
        {
            GroupName = "default-consumer-group",
            Topics = new[] { "user-events", "order-events" }
        },
        new ConsumerGroupConfig
        {
            GroupName = "analytics-group",
            Topics = new[] { "analytics-events" }
        },
        new ConsumerGroupConfig
        {
            GroupName = "notification-group",
            Topics = new[] { "notification-events" }
        }
    };

// Configure Quartz.NET
builder.Services.AddQuartz(q =>
{
    // Consumer heartbeat job
    var heartbeatJobKey = new JobKey("ConsumerHeartbeat");
    q.AddJob<ConsumerHeartbeatJob>(opts => opts.WithIdentity(heartbeatJobKey));
    q.AddTrigger(opts => opts
        .ForJob(heartbeatJobKey)
        .WithIdentity("ConsumerHeartbeat-trigger")
        .WithSimpleSchedule(x => x
            .WithIntervalInSeconds(30)
            .RepeatForever()));

    // Add consumer jobs for each consumer group
    foreach (var consumerGroup in consumerGroups)
    {
        var jobKey = new JobKey($"Consumer-{consumerGroup.GroupName}");
        q.AddJob<ConsumerJob>(opts => opts
            .WithIdentity(jobKey)
            .UsingJobData("ConsumerGroup", consumerGroup.GroupName)
            .UsingJobData("Topics", string.Join(",", consumerGroup.Topics)));

        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity($"Consumer-{consumerGroup.GroupName}-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(5) // Poll every 5 seconds
                .RepeatForever()));
    }
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Logging
builder.Logging.AddConsole();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ConsumerDbContext>();
    await context.Database.EnsureCreatedAsync();

    // Register this consumer service instance with the producer service
    await RegisterConsumerAgentAsync(serviceId, instanceId, builder.Configuration);
}

async Task RegisterConsumerAgentAsync(string serviceId, string instanceId, IConfiguration configuration)
{
    try
    {
        using var httpClient = new HttpClient();
        var producerServiceUrl = configuration["ProducerService:BaseUrl"] ?? "http://localhost:5299";

        var hostName = Environment.MachineName;
        var ipAddress = Dns.GetHostAddresses(hostName)
            .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString()
            ?? "127.0.0.1";

        var port = configuration.GetValue<int?>("Port")
            ?? (int.TryParse(configuration["urls"]?.Split(':').Last().Split(';').First(), out var p) ? p : 5156); var consumerGroups = configuration.GetSection("ConsumerGroups").Get<ConsumerService.Models.ConsumerGroupConfig[]>()
            ?? Array.Empty<ConsumerService.Models.ConsumerGroupConfig>();

        var assignedGroups = consumerGroups.Select(cg => cg.GroupName).ToArray();
        var assignedTopics = consumerGroups.SelectMany(cg => cg.Topics).Distinct().ToArray();

        var registration = new AgentRegistrationRequest
        {
            ServiceId = serviceId,
            ServiceName = "Consumer Service",
            HostName = hostName,
            IpAddress = ipAddress,
            Port = port,
            BaseUrl = $"http://{ipAddress}:{port}",
            ServiceType = ServiceType.Consumer,
            Version = "1.0.0",
            AssignedConsumerGroups = assignedGroups,
            AssignedTopics = assignedTopics,
            Metadata = new Dictionary<string, string>
            {
                ["Environment"] = app.Environment.EnvironmentName,
                ["StartTime"] = DateTime.UtcNow.ToString("O"),
                ["MachineName"] = Environment.MachineName,
                ["ProcessId"] = Environment.ProcessId.ToString()
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(registration);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await httpClient.PostAsync($"{producerServiceUrl}/api/agents/consumers/register", content);

        if (response.IsSuccessStatusCode)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("Consumer service {ServiceId} registered successfully with producer service", serviceId);
        }
        else
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Failed to register consumer service {ServiceId} with producer service. Status: {Status}",
                serviceId, response.StatusCode);
        }
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error registering consumer service {ServiceId} with producer service", serviceId);
    }
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
