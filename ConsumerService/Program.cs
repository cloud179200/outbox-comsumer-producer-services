using Microsoft.EntityFrameworkCore;
using ConsumerService.Services;
using ConsumerService.BackgroundServices;
using ConsumerService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();

// PostgreSQL configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=outbox_db;Username=outbox_user;Password=outbox_password";
builder.Services.AddDbContext<ConsumerDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register services
builder.Services.AddScoped<IKafkaConsumerService, KafkaConsumerService>();
builder.Services.AddScoped<IMessageProcessor, MessageProcessor>();
builder.Services.AddScoped<IConsumerTrackingService, ConsumerPostgreSqlTrackingService>();
builder.Services.AddHttpClient();

// Background services for consumers
builder.Services.AddHostedService<ConsumerBackgroundService>();

// Logging
builder.Logging.AddConsole();

var app = builder.Build();

// Ensure database is created and migrations are applied
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ConsumerDbContext>();
    await context.Database.EnsureCreatedAsync();
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
