using Quartz;
using ConsumerService.Services;
using ConsumerService.Models;

namespace ConsumerService.Jobs;

[DisallowConcurrentExecution]
public class ConsumerJob : IJob
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<ConsumerJob> _logger;
  private readonly IConfiguration _configuration;

  public ConsumerJob(
      IServiceProvider serviceProvider,
      ILogger<ConsumerJob> logger,
      IConfiguration configuration)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _configuration = configuration;
  }

  public async Task Execute(IJobExecutionContext context)
  {
    var jobData = context.JobDetail.JobDataMap;
    var consumerGroup = jobData.GetString("ConsumerGroup") ?? "default-consumer-group";
    var topics = jobData.GetString("Topics")?.Split(',') ?? new[] { "user-events" };

    _logger.LogDebug("Consumer job started for group {ConsumerGroup} with topics: [{Topics}]",
        consumerGroup, string.Join(", ", topics));

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var kafkaConsumerService = scope.ServiceProvider.GetRequiredService<IKafkaConsumerService>();

      // Create a cancellation token that will be cancelled when the job should stop
      using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // Poll for 30 seconds max per job execution

      await kafkaConsumerService.ConsumeMessagesAsync(topics, consumerGroup, cts.Token);
    }
    catch (OperationCanceledException)
    {
      _logger.LogDebug("Consumer job cancelled for group {ConsumerGroup}", consumerGroup);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in consumer job for group {ConsumerGroup}", consumerGroup);
    }

    _logger.LogDebug("Consumer job completed for group {ConsumerGroup}", consumerGroup);
  }
}
