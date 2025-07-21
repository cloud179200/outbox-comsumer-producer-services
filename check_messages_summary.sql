SELECT
  COUNT(*) as total_messages,
  "Status",
  MAX("CreatedAt") as latest
FROM
  "OutboxMessages"
GROUP BY
  "Status"
ORDER BY
  "Status";