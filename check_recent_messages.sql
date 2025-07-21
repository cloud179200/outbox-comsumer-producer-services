SELECT
  "Id",
  "Status",
  "CreatedAt",
  "ErrorMessage",
  "RetryCount"
FROM
  "OutboxMessages"
WHERE
  "CreatedAt" > NOW () - INTERVAL '10 minutes'
ORDER BY
  "CreatedAt" DESC
LIMIT
  10;