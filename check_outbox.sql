SELECT
  "Id",
  "Topic",
  "Message",
  "Status",
  "CreatedAt",
  "ProcessedAt"
FROM
  "OutboxMessages"
ORDER BY
  "CreatedAt" DESC
LIMIT
  5;