-- Initialize databases for the outbox pattern implementation

-- Create the outbox database for the producer service
CREATE DATABASE outbox_db;
CREATE USER outbox_user WITH PASSWORD 'outbox_password';
GRANT ALL PRIVILEGES ON DATABASE outbox_db TO outbox_user;
ALTER USER outbox_user CREATEDB;

-- Create the consumer database for the consumer service
CREATE DATABASE consumer_db;
CREATE USER consumer_user WITH PASSWORD 'consumer_password';
GRANT ALL PRIVILEGES ON DATABASE consumer_db TO consumer_user;
ALTER USER consumer_user CREATEDB;

-- Connect to outbox_db and set up schema
\c outbox_db;
GRANT ALL ON SCHEMA public TO outbox_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO outbox_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO outbox_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO outbox_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO outbox_user;

-- Connect to consumer_db and set up schema
\c consumer_db;
GRANT ALL ON SCHEMA public TO consumer_user;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO consumer_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO consumer_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON TABLES TO consumer_user;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL ON SEQUENCES TO consumer_user;
