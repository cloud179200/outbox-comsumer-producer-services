SELECT
  "MessageId",
  "ConsumerGroupRegistrationId",
  "Success",
  "AcknowledgedAt",
  "ErrorMessage"
FROM
  "ConsumerAcknowledgments"
ORDER BY
  "AcknowledgedAt" DESC
LIMIT
  5;