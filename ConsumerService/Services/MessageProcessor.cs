using ConsumerService.Models;

namespace ConsumerService.Services;

public interface IMessageProcessor
{
  Task<bool> ProcessMessageAsync(ConsumerMessage message);
}

public class MessageProcessor : IMessageProcessor
{
  private readonly ILogger<MessageProcessor> _logger;
  private readonly IConsumerTrackingService _consumerTracking;

  /// <summary>
  /// Initializes a new instance of MessageProcessor with required dependencies.
  /// Sets up logging and consumer tracking services for message processing operations.
  /// </summary>
  /// <param name="logger">Logger for tracking message processing operations</param>
  /// <param name="consumerTracking">Service for tracking processed and failed messages</param>
  public MessageProcessor(ILogger<MessageProcessor> logger, IConsumerTrackingService consumerTracking)
  {
    _logger = logger;
    _consumerTracking = consumerTracking;
  }

  /// <summary>
  /// Processes a consumer message with idempotency checks and error handling.
  /// Ensures messages are processed only once per consumer group and tracks processing status.
  /// </summary>
  /// <param name="message">The message to process, including topic, content, and consumer group information</param>
  /// <returns>True if message was processed successfully, false if processing failed</returns>
  public async Task<bool> ProcessMessageAsync(ConsumerMessage message)
  {
    try
    {
      _logger.LogInformation("Processing message {MessageId} from topic {Topic} for consumer group {ConsumerGroup}",
          message.MessageId, message.Topic, message.ConsumerGroup);

      // Check if message was already processed (idempotency) - check by both messageId and idempotencyKey
      if (await _consumerTracking.IsMessageProcessedAsync(message.MessageId, message.ConsumerGroup))
      {
        _logger.LogInformation("Message {MessageId} already processed by consumer group {ConsumerGroup}, skipping",
            message.MessageId, message.ConsumerGroup);
        return true;
      }

      // Also check by idempotency key if provided
      if (!string.IsNullOrEmpty(message.IdempotencyKey) &&
          await _consumerTracking.IsMessageProcessedByIdempotencyKeyAsync(message.IdempotencyKey, message.ConsumerGroup))
      {
        _logger.LogInformation("Message with idempotency key {IdempotencyKey} already processed by consumer group {ConsumerGroup}, skipping",
            message.IdempotencyKey, message.ConsumerGroup);
        return true;
      }

      // Mark message as being processed
      await _consumerTracking.MarkMessageAsProcessingAsync(message);

      // Simulate actual message processing based on topic
      var success = await ProcessByTopic(message); if (success)
      {
        // Mark message as successfully processed
        await _consumerTracking.MarkMessageAsProcessedAsync(
            message.MessageId,
            message.ConsumerGroup,
            message.Topic,
            message.Content,
            message.ProducerServiceId,
            message.ProducerInstanceId,
            message.IdempotencyKey);
        _logger.LogInformation("Message {MessageId} processed successfully", message.MessageId);
      }
      else
      {
        // Mark message as failed
        await _consumerTracking.MarkMessageAsFailedAsync(
            message.MessageId,
            message.ConsumerGroup,
            message.Topic,
            "Processing failed",
            message.Content,
            message.ProducerServiceId,
            message.ProducerInstanceId);
        _logger.LogWarning("Message {MessageId} processing failed", message.MessageId);
      }

      return success;
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
      await _consumerTracking.MarkMessageAsFailedAsync(
          message.MessageId,
          message.ConsumerGroup,
          message.Topic,
          ex.Message,
          message.Content,
          message.ProducerServiceId,
          message.ProducerInstanceId);
      return false;
    }
  }

  /// <summary>
  /// Routes message processing based on topic type for specialized handling.
  /// Simulates different processing logic and timing for various message types.
  /// </summary>
  /// <param name="message">The message containing topic and content information</param>
  /// <returns>True if topic-specific processing was successful, false otherwise</returns>
  private async Task<bool> ProcessByTopic(ConsumerMessage message)
  {
    // Simulate different processing logic based on topic
    await Task.Delay(100); // Simulate processing time

    return message.Topic switch
    {
      "user-events" => await ProcessUserEvent(message),
      "order-events" => await ProcessOrderEvent(message),
      "notification-events" => await ProcessNotificationEvent(message),
      "analytics-events" => await ProcessAnalyticsEvent(message),
      _ => await ProcessGenericMessage(message)
    };
  }

  /// <summary>
  /// Processes user-related events with simulated business logic.
  /// Handles user registration, profile updates, and authentication events.
  /// </summary>
  /// <param name="message">The user event message to process</param>
  /// <returns>True indicating successful user event processing</returns>
  private async Task<bool> ProcessUserEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing user event: {Content}", message.Content);
    // Simulate user event processing
    await Task.Delay(50);
    return true;
  }

  /// <summary>
  /// Processes order-related events with simulated e-commerce logic.
  /// Handles order creation, updates, payments, and fulfillment events.
  /// </summary>
  /// <param name="message">The order event message to process</param>
  /// <returns>True indicating successful order event processing</returns>
  private async Task<bool> ProcessOrderEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing order event: {Content}", message.Content);
    // Simulate order event processing
    await Task.Delay(75);
    return true;
  }

  /// <summary>
  /// Processes notification events for rapid delivery to users.
  /// Handles email, SMS, push notifications, and system alerts.
  /// </summary>
  /// <param name="message">The notification event message to process</param>
  /// <returns>True indicating successful notification processing</returns>
  private async Task<bool> ProcessNotificationEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing notification event: {Content}", message.Content);
    // Simulate notification event processing
    await Task.Delay(25);
    return true;
  }

  /// <summary>
  /// Processes analytics events for data analysis and reporting.
  /// Handles user behavior tracking, performance metrics, and business intelligence data.
  /// </summary>
  /// <param name="message">The analytics event message to process</param>
  /// <returns>True indicating successful analytics event processing</returns>
  private async Task<bool> ProcessAnalyticsEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing analytics event: {Content}", message.Content);
    // Simulate analytics event processing
    await Task.Delay(100);
    return true;
  }

  private async Task<bool> ProcessGenericMessage(ConsumerMessage message)
  {
    _logger.LogInformation("Processing generic message: {Content}", message.Content);
    // Simulate generic message processing
    await Task.Delay(50);
    return true;
  }
}
