#!/usr/bin/env bash
# Restore a backup made by backup-mysql.sh.
#
#   Check a backup without touching the real data (do this regularly, a backup that was never restored is only a hope):
#     MYSQL_HOST=... MYSQL_USER=... MYSQL_PWD=... ./ops/restore-mysql.sh --verify /var/backups/textileconnect/textileconnect-20261117T020000Z.sql.gz
#   Put a backup in the real database (it ERASES what is there):
#     ./ops/restore-mysql.sh --into textileconnect /var/backups/textileconnect/textileconnect-20261117T020000Z.sql.gz
#
# --verify restores into a throw-away database named restore_check_<time>, counts the tables, foreign keys and rows of the main tables, prints them, and drops the database again.
# It exits with an error when the restore fails or the tables are missing. The user needs CREATE and DROP rights.
# Not tested against a server here (none was available when it was written).
set -euo pipefail

mode="${1:?Usage: restore-mysql.sh --verify <file.sql.gz> | --into <database> <file.sql.gz>}"
HOST="${MYSQL_HOST:-127.0.0.1}"
PORT="${MYSQL_PORT:-3306}"
USER_NAME="${MYSQL_USER:-root}"
auth=()
if [[ -n "${MYSQL_DEFAULTS_FILE:-}" ]]; then auth+=("--defaults-extra-file=${MYSQL_DEFAULTS_FILE}"); fi
client() { mysql "${auth[@]}" --host="$HOST" --port="$PORT" --user="$USER_NAME" --default-character-set=utf8mb4 "$@"; }

case "$mode" in
  --verify)
    file="${2:?give the backup file}"
    gzip -t "$file"
    scratch="restore_check_$(date -u +%Y%m%d%H%M%S)"
    trap 'client -e "DROP DATABASE IF EXISTS \`'"$scratch"'\`" || true' EXIT
    client -e "CREATE DATABASE \`$scratch\` CHARACTER SET utf8mb4"
    gunzip -c "$file" | client "$scratch"
    echo "Restored into $scratch. Checking:"
    client -N "$scratch" -e "
      SELECT 'tables', COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';
      SELECT 'foreign keys', COUNT(*) FROM information_schema.TABLE_CONSTRAINTS WHERE CONSTRAINT_SCHEMA = DATABASE() AND CONSTRAINT_TYPE = 'FOREIGN KEY';
      SELECT 'applied migrations', COUNT(*) FROM __EFMigrationsHistory;
      SELECT 'businesses', COUNT(*) FROM Businesses;
      SELECT 'products', COUNT(*) FROM Products;
      SELECT 'orders', COUNT(*) FROM Orders;
      SELECT 'payments', COUNT(*) FROM Payments;
      SELECT 'settlements', COUNT(*) FROM Settlements;"
    tables="$(client -N "$scratch" -e "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'")"
    if (( tables < 20 )); then echo "Only $tables tables came back: the backup is incomplete." >&2; exit 1; fi
    echo "The backup restores. (The scratch database $scratch is dropped now.)"
    ;;
  --into)
    db="${2:?give the database}"; file="${3:?give the backup file}"
    gzip -t "$file"
    read -r -p "This ERASES the tables of '$db' on $HOST and loads $file. Type the database name to go on: " answer
    [[ "$answer" == "$db" ]] || { echo "Cancelled."; exit 1; }
    gunzip -c "$file" | client "$db"
    echo "Restored $file into $db. Start the API again and check /api/v1/health/ready."
    ;;
  *) echo "Unknown option $mode" >&2; exit 2 ;;
esac
