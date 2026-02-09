#!/usr/bin/env sh
set -eu

SCRIPT_PATH="/app/migrations.sql"

if [ ! -f "$SCRIPT_PATH" ]; then
  echo "Migration script not found at $SCRIPT_PATH"
  exit 1
fi

if [ -n "${MYSQLHOST:-}" ] && [ -n "${MYSQLUSER:-}" ] && [ -n "${MYSQLDATABASE:-}" ]; then
  echo "Applying migrations via MYSQLHOST/MYSQLUSER/MYSQLDATABASE..."
  mysql \
    --protocol=TCP \
    --host="${MYSQLHOST}" \
    --port="${MYSQLPORT:-3306}" \
    --user="${MYSQLUSER}" \
    --password="${MYSQLPASSWORD:-}" \
    "${MYSQLDATABASE}" < "$SCRIPT_PATH"
  exit 0
fi

if [ -n "${MYSQL_URL:-}" ]; then
  echo "Applying migrations via MYSQL_URL..."

  url_no_scheme="${MYSQL_URL#mysql://}"
  url_no_scheme="${url_no_scheme#mysql2://}"
  url_main="${url_no_scheme%%\?*}"

  credentials_and_host="${url_main%%/*}"
  database_name="${url_main#*/}"

  if [ "$credentials_and_host" = "$url_main" ] || [ -z "$database_name" ]; then
    echo "MYSQL_URL format is invalid."
    exit 1
  fi

  credentials="${credentials_and_host%@*}"
  host_and_port="${credentials_and_host#*@}"

  if [ "$credentials" = "$credentials_and_host" ]; then
    echo "MYSQL_URL must include user credentials."
    exit 1
  fi

  mysql_user="${credentials%%:*}"
  mysql_password="${credentials#*:}"
  mysql_host="${host_and_port%%:*}"
  mysql_port="${host_and_port#*:}"

  if [ "$mysql_port" = "$host_and_port" ]; then
    mysql_port="3306"
  fi

  mysql \
    --protocol=TCP \
    --host="$mysql_host" \
    --port="$mysql_port" \
    --user="$mysql_user" \
    --password="$mysql_password" \
    "$database_name" < "$SCRIPT_PATH"
  exit 0
fi

echo "No supported MySQL connection variables found. Set MYSQL_URL or MYSQLHOST/MYSQLUSER/MYSQLPASSWORD/MYSQLDATABASE."
exit 1
