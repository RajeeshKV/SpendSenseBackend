#!/usr/bin/env sh
set -eu

if [ -n "${PORT:-}" ]; then
  export ASPNETCORE_URLS="http://0.0.0.0:${PORT}"
fi

if [ "${RUN_EF_MIGRATIONS:-true}" = "true" ] && [ -x "./migrate" ]; then
  echo "Applying EF Core migrations..."
  ./migrate --connection "$ConnectionStrings__DefaultConnection"
fi

echo "Starting SpendSense API..."
exec dotnet SpendSense.Api.dll
