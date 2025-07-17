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

  public MessageProcessor(ILogger<MessageProcessor> logger, IConsumerTrackingService consumerTracking)
  {
    _logger = logger;
    _consumerTracking = consumerTracking;
  }

  public async Task<bool> ProcessMessageAsync(ConsumerMessage message)
  {
    try
    {
      _logger.LogInformation("Processing message {MessageId} from topic {Topic} for consumer group {ConsumerGroup}",
          message.MessageId, message.Topic, message.ConsumerGroup);

      // Check if message was already processed (idempotency)
      if (await _consumerTracking.IsMessageProcessedAsync(message.MessageId, message.ConsumerGroup))
      {
        _logger.LogInformation("Message {MessageId} already processed by consumer group {ConsumerGroup}, skipping",
            message.MessageId, message.ConsumerGroup);
        return true;
      }

      // Mark message as being processed
      await _consumerTracking.MarkMessageAsProcessingAsync(message);

      // Simulate actual message processing based on topic
      var success = await ProcessByTopic(message);

      if (success)
      {
        // Mark message as successfully processed
        await _consumerTracking.MarkMessageAsProcessedAsync(
            message.MessageId,
            message.ConsumerGroup,
            message.Topic,
            message.Content);
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
            message.Content);
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
          message.Content);
      return false;
    }
  }

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

  private async Task<bool> ProcessUserEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing user event: {Content}", message.Content);
    // Simulate user event processing
    await Task.Delay(50);
    return true;
  }

  private async Task<bool> ProcessOrderEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing order event: {Content}", message.Content);
    // Simulate order event processing
    await Task.Delay(75);
    return true;
  }

  private async Task<bool> ProcessNotificationEvent(ConsumerMessage message)
  {
    _logger.LogInformation("Processing notification event: {Content}", message.Content);
    // Simulate notification event processing
    await Task.Delay(25);
    return true;
  }

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
