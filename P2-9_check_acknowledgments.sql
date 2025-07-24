-- ConsumerAcknowledgments table has been removed from the system
-- This query is no longer applicable as acknowledgments are tracked through message status only
SELECT
  'ConsumerAcknowledgments table removed - use OutboxMessages status instead' as Notice;