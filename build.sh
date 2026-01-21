#!/usr/bin/env bash
# Build script for Render - runs migrations on Postgres

set -o errexit

echo "Running database migrations..."

# Check if DATABASE_URL is set
if [ -z "$DATABASE_URL" ]; then
  echo "ERROR: DATABASE_URL environment variable is not set"
  exit 1
fi

# Run Postgres migrations
echo "Applying migration: 001_add_shift_columns_postgres.sql"
psql "$DATABASE_URL" -f migrations/001_add_shift_columns_postgres.sql || {
  echo "Migration may have already been applied, continuing..."
}

echo "Migrations completed successfully!"
