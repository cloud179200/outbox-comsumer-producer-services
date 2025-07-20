using ProducerService.Models;
using System.Collections.Concurrent;

namespace ProducerService.Services;

public interface IQuartzMessageBatchingService
{
  Task<string> QueueMessageAsync(MessageRequest request);
  Task FlushBatchAsync();
  Task<MessageResponse> ProcessImmediateAsync(MessageRequest request);
}

public class QuartzMessageBatchingService : IQuartzMessageBatchingService
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<QuartzMessageBatchingService> _logger;
  private readonly ConcurrentQueue<MessageRequest> _messageQueue;
  private readonly object _batchLock = new object();

  private const int BATCH_SIZE = 500;

  public QuartzMessageBatchingService(IServiceProvider serviceProvider, ILogger<QuartzMessageBatchingService> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;
    _messageQueue = new ConcurrentQueue<MessageRequest>();
  }

  public Task<string> QueueMessageAsync(MessageRequest request)
  {
    var messageId = Guid.NewGuid().ToString();
    _messageQueue.Enqueue(request);

    _logger.LogDebug("Message queued for batch processing on topic {Topic}. Queue size: {QueueSize}",
        request.Topic, _messageQueue.Count);

    // Check if we should flush immediately due to batch size
    if (_messageQueue.Count >= BATCH_SIZE)
    {
      _logger.LogInformation("Batch size reached ({BatchSize}), triggering immediate flush", BATCH_SIZE);
      _ = Task.Run(async () => await FlushBatchAsync());
    }

    return Task.FromResult(messageId);
  }

  public async Task FlushBatchAsync()
  {
    if (_messageQueue.IsEmpty)
      return;

    List<MessageRequest> batchToProcess;

    lock (_batchLock)
    {
      batchToProcess = new List<MessageRequest>();

      // Dequeue all messages up to batch size
      while (batchToProcess.Count < BATCH_SIZE && _messageQueue.TryDequeue(out var message))
      {
        batchToProcess.Add(message);
      }
    }

    if (!batchToProcess.Any())
      return;

    _logger.LogInformation("Processing batch of {BatchSize} messages", batchToProcess.Count);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

      // Use bulk insert to create all messages at once
      var allOutboxMessages = await outboxService.CreateMessagesBulkAsync(batchToProcess);

      stopwatch.Stop();
      _logger.LogInformation("Batch of {BatchSize} messages processed in {ElapsedMs}ms. Created {OutboxCount} outbox messages",
          batchToProcess.Count, stopwatch.ElapsedMilliseconds, allOutboxMessages.Count);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Critical error processing batch of {BatchSize} messages", batchToProcess.Count);

      // Re-queue failed messages for retry
      foreach (var message in batchToProcess)
      {
        _messageQueue.Enqueue(message);
      }
    }
  }

  public async Task<MessageResponse> ProcessImmediateAsync(MessageRequest request)
  {
    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

      var messages = await outboxService.CreateMessagesForTopicAsync(
          request.Topic,
          request.Message,
          request.ConsumerGroup);

      if (!messages.Any())
      {
        throw new InvalidOperationException($"No registered consumer groups found for topic '{request.Topic}'");
      }

      _logger.LogInformation("Created {Count} outbox messages for immediate processing on topic {Topic}",
          messages.Count, request.Topic);

      return new MessageResponse
      {
        MessageId = messages.First().Id,
        Status = "Queued",
        Topic = request.Topic,
        TargetConsumerGroups = messages.Select(m => m.ConsumerGroup).ToList(),
        ProducerServiceId = messages.First().ProducerServiceId,
        ProducerInstanceId = messages.First().ProducerInstanceId
      };
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing immediate message for topic {Topic}", request.Topic);
      throw;
    }
  }
}
