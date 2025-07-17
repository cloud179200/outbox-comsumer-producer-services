-- Check if Consumer Service tables exist
SELECT
  schemaname,
  tablename
FROM
  pg_tables
WHERE
  schemaname = 'public'
  AND tablename LIKE '%message%';

-- Check all Consumer Service tables
SELECT
  schemaname,
  tablename
FROM
  pg_tables
WHERE
  schemaname = 'public';