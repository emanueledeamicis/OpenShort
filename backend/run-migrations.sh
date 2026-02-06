#!/bin/bash
set -e

echo "=== OpenShort Database Migration Script ==="

# Wait for MySQL to be ready
echo "Waiting for MySQL to be ready..."
max_retries=30
retry_count=0

cd /src/OpenShort.Api

while [ $retry_count -lt $max_retries ]; do
  if dotnet ef database update --project ../OpenShort.Infrastructure --configuration Release 2>&1; then
    echo "=== Database migrations completed successfully ==="
    exit 0
  fi
  
  echo "Migration attempt $((retry_count + 1))/$max_retries failed. Retrying in 2 seconds..."
  sleep 2
  retry_count=$((retry_count + 1))
done

echo "ERROR: Failed to apply migrations after $max_retries attempts"
exit 1
