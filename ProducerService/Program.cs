using Microsoft.EntityFrameworkCore;
using ProducerService.Services;
using ProducerService.BackgroundServices;
using ProducerService.Data;
using ProducerService.Models;
using System.Net;

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

// PostgreSQL configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password";
builder.Services.AddDbContext<OutboxDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register services
builder.Services.AddScoped<IOutboxService, OutboxPostgreSqlService>();
builder.Services.AddScoped<IKafkaProducerService, KafkaProducerService>();
builder.Services.AddScoped<ITopicRegistrationService, TopicRegistrationService>();
builder.Services.AddScoped<IAgentService, AgentService>();
builder.Services.AddHttpClient();

// Background services
builder.Services.AddHostedService<OutboxProcessorService>();
builder.Services.AddHostedService<AgentHeartbeatService>();

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
    var ipAddress = Dns.GetHostAddresses(hostName)
        .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?.ToString()
        ?? "127.0.0.1";
    var port = builder.Configuration.GetValue<int?>("Port")
      ?? (int.TryParse(builder.Configuration["urls"]?.Split(':').Last().Split(';').First(), out var p) ? p : 5299);

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
