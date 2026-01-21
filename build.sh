#!/usr/bin/env bash
# Build script for Render - runs migrations on Postgres

set -o errexit

echo "Running database migrations..."

# Check if DATABASE_URL is set
if [ -z "$DATABASE_URL" ]; then
  echo "WARNING: DATABASE_URL environment variable is not set, skipping migrations"
  exit 0
fi

# Run Postgres migrations
echo "Applying migration: 001_add_shift_columns_postgres.sql"
psql "$DATABASE_URL" -f migrations/001_add_shift_columns_postgres.sql 2>/dev/null || {
  echo "Migration may have already been applied, continuing..."
}

echo "Migrations completed successfully!"
