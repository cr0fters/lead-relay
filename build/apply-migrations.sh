#!/usr/bin/env sh
set -eu

SCRIPT_PATH="/app/migrations.sql"

if [ ! -f "$SCRIPT_PATH" ]; then
  echo "Migration script not found at $SCRIPT_PATH"
  exit 1
fi

if [ -n "${MYSQL_URL:-}" ]; then
  echo "Applying migrations via MYSQL_URL..."
  mysql "$MYSQL_URL" < "$SCRIPT_PATH"
  exit 0
fi

if [ -n "${MYSQLHOST:-}" ] && [ -n "${MYSQLUSER:-}" ] && [ -n "${MYSQLDATABASE:-}" ]; then
  echo "Applying migrations via MYSQLHOST/MYSQLUSER/MYSQLDATABASE..."
  mysql \
    --host="${MYSQLHOST}" \
    --port="${MYSQLPORT:-3306}" \
    --user="${MYSQLUSER}" \
    --password="${MYSQLPASSWORD:-}" \
    "${MYSQLDATABASE}" < "$SCRIPT_PATH"
  exit 0
fi

echo "No supported MySQL connection variables found. Set MYSQL_URL or MYSQLHOST/MYSQLUSER/MYSQLPASSWORD/MYSQLDATABASE."
exit 1
