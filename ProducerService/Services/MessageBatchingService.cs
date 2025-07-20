using ProducerService.Models;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ProducerService.Services;

public interface IMessageBatchingService
{
  Task<string> QueueMessageAsync(MessageRequest request);
  Task FlushBatchAsync();
  Task StartBatchProcessingAsync(CancellationToken cancellationToken);
}

public class MessageBatchingService : IMessageBatchingService, IDisposable
{
  private readonly IServiceProvider _serviceProvider;
  private readonly ILogger<MessageBatchingService> _logger;
  private readonly Channel<BatchedMessageItem> _messageQueue;
  private readonly ChannelWriter<BatchedMessageItem> _writer;
  private readonly ChannelReader<BatchedMessageItem> _reader;
  private readonly Timer _flushTimer;
  private readonly SemaphoreSlim _batchLock;
  private readonly ConcurrentDictionary<string, TaskCompletionSource<MessageResponse>> _pendingResponses;

  private const int BATCH_SIZE = 500;
  private const int FLUSH_INTERVAL_SECONDS = 30;

  private readonly List<BatchedMessageItem> _currentBatch;
  private volatile bool _disposed;

  public MessageBatchingService(IServiceProvider serviceProvider, ILogger<MessageBatchingService> logger)
  {
    _serviceProvider = serviceProvider;
    _logger = logger;

    var options = new BoundedChannelOptions(10000)
    {
      FullMode = BoundedChannelFullMode.Wait,
      SingleReader = true,
      SingleWriter = false
    };

    var channel = Channel.CreateBounded<BatchedMessageItem>(options);
    _messageQueue = channel;
    _writer = channel.Writer;
    _reader = channel.Reader;

    _currentBatch = new List<BatchedMessageItem>();
    _batchLock = new SemaphoreSlim(1, 1);
    _pendingResponses = new ConcurrentDictionary<string, TaskCompletionSource<MessageResponse>>();

    // Timer to flush batches every 30 seconds
    _flushTimer = new Timer(async _ => await FlushBatchAsync(), null,
        TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS),
        TimeSpan.FromSeconds(FLUSH_INTERVAL_SECONDS));
  }

  public async Task<string> QueueMessageAsync(MessageRequest request)
  {
    if (_disposed)
      throw new ObjectDisposedException(nameof(MessageBatchingService));

    var messageId = Guid.NewGuid().ToString();
    var tcs = new TaskCompletionSource<MessageResponse>();
    _pendingResponses[messageId] = tcs;

    var batchItem = new BatchedMessageItem
    {
      Id = messageId,
      Request = request,
      QueuedAt = DateTime.UtcNow,
      CompletionSource = tcs
    };

    await _writer.WriteAsync(batchItem);
    _logger.LogDebug("Message {MessageId} queued for batching", messageId);

    return messageId;
  }

  public async Task StartBatchProcessingAsync(CancellationToken cancellationToken)
  {
    _logger.LogInformation("Starting message batch processing");

    try
    {
      await foreach (var item in _reader.ReadAllAsync(cancellationToken))
      {
        await _batchLock.WaitAsync(cancellationToken);
        try
        {
          _currentBatch.Add(item);
          _logger.LogDebug("Added message {MessageId} to batch. Current batch size: {BatchSize}",
              item.Id, _currentBatch.Count);

          // Check if we need to flush the batch
          if (_currentBatch.Count >= BATCH_SIZE)
          {
            _logger.LogInformation("Batch size limit reached ({BatchSize}), flushing batch", BATCH_SIZE);
            await ProcessCurrentBatchAsync();
          }
        }
        finally
        {
          _batchLock.Release();
        }
      }
    }
    catch (OperationCanceledException)
    {
      _logger.LogInformation("Batch processing cancelled");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error in batch processing");
    }
    finally
    {
      // Flush any remaining messages
      await FlushBatchAsync();
    }
  }

  public async Task FlushBatchAsync()
  {
    if (_disposed || _currentBatch.Count == 0)
      return;

    await _batchLock.WaitAsync();
    try
    {
      if (_currentBatch.Count > 0)
      {
        _logger.LogInformation("Timer flush triggered with {BatchSize} messages", _currentBatch.Count);
        await ProcessCurrentBatchAsync();
      }
    }
    finally
    {
      _batchLock.Release();
    }
  }

  private async Task ProcessCurrentBatchAsync()
  {
    if (_currentBatch.Count == 0)
      return;

    var batchToProcess = new List<BatchedMessageItem>(_currentBatch);
    _currentBatch.Clear();

    _logger.LogInformation("Processing batch of {BatchSize} messages", batchToProcess.Count);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    try
    {
      using var scope = _serviceProvider.CreateScope();
      var outboxService = scope.ServiceProvider.GetRequiredService<IOutboxService>();

      // Prepare all message requests for bulk processing
      var messageRequests = batchToProcess.Select(item => item.Request).ToList();

      _logger.LogInformation("Starting bulk creation of {RequestCount} message requests", messageRequests.Count);

      // Create all messages in a single bulk operation
      var allOutboxMessages = await outboxService.CreateMessagesBulkAsync(messageRequests);

      _logger.LogInformation("Bulk created {OutboxCount} outbox messages from {RequestCount} requests",
          allOutboxMessages.Count, messageRequests.Count);

      // Build responses based on created messages
      var responses = new Dictionary<string, MessageResponse>();
      var messagesByTopic = allOutboxMessages.GroupBy(m => m.Topic).ToDictionary(g => g.Key, g => g.ToList());

      foreach (var item in batchToProcess)
      {
        try
        {
          if (messagesByTopic.TryGetValue(item.Request.Topic, out var topicMessages))
          {
            // Find messages for this specific request based on consumer group filter
            var relevantMessages = item.Request.ConsumerGroup != null
                ? topicMessages.Where(m => m.ConsumerGroup == item.Request.ConsumerGroup).ToList()
                : topicMessages;

            if (relevantMessages.Any())
            {
              var response = new MessageResponse
              {
                MessageId = relevantMessages.First().Id,
                Status = "Queued",
                Topic = item.Request.Topic,
                TargetConsumerGroups = relevantMessages.Select(m => m.ConsumerGroup).ToList(),
                ProducerServiceId = relevantMessages.First().ProducerServiceId,
                ProducerInstanceId = relevantMessages.First().ProducerInstanceId
              };
              responses[item.Id] = response;
            }
            else
            {
              responses[item.Id] = new MessageResponse
              {
                MessageId = item.Id,
                Status = "Failed",
                Topic = item.Request.Topic,
                TargetConsumerGroups = new List<string>()
              };
            }
          }
          else
          {
            responses[item.Id] = new MessageResponse
            {
              MessageId = item.Id,
              Status = "Failed",
              Topic = item.Request.Topic,
              TargetConsumerGroups = new List<string>()
            };
          }
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Error processing message {MessageId} in batch", item.Id);
          responses[item.Id] = new MessageResponse
          {
            MessageId = item.Id,
            Status = "Failed",
            Topic = item.Request.Topic,
            TargetConsumerGroups = new List<string>()
          };
        }
      }

      stopwatch.Stop();
      _logger.LogInformation("Batch of {BatchSize} messages processed in {ElapsedMs}ms. Created {OutboxCount} outbox messages",
          batchToProcess.Count, stopwatch.ElapsedMilliseconds, allOutboxMessages.Count);

      // Complete all the TaskCompletionSources
      foreach (var item in batchToProcess)
      {
        if (_pendingResponses.TryRemove(item.Id, out var tcs))
        {
          if (responses.TryGetValue(item.Id, out var response))
          {
            tcs.SetResult(response);
          }
          else
          {
            tcs.SetException(new InvalidOperationException("Response not found for message"));
          }
        }
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Critical error processing batch");

      // Fail all pending requests in this batch
      foreach (var item in batchToProcess)
      {
        if (_pendingResponses.TryRemove(item.Id, out var tcs))
        {
          tcs.SetException(ex);
        }
      }
    }
  }

  public void Dispose()
  {
    if (_disposed)
      return;

    _disposed = true;

    _flushTimer?.Dispose();
    _writer?.TryComplete();
    _batchLock?.Dispose();

    // Complete any remaining pending responses with cancellation
    foreach (var kvp in _pendingResponses)
    {
      if (_pendingResponses.TryRemove(kvp.Key, out var tcs))
      {
        tcs.SetCanceled();
      }
    }
  }
}

public class BatchedMessageItem
{
  public string Id { get; set; } = string.Empty;
  public MessageRequest Request { get; set; } = null!;
  public DateTime QueuedAt { get; set; }
  public TaskCompletionSource<MessageResponse> CompletionSource { get; set; } = null!;
}
