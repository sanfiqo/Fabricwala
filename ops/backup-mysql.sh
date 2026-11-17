#!/usr/bin/env bash
# Daily backup of the TextileConnect MySQL databases.
#
#   MYSQL_HOST=db.internal MYSQL_USER=backup MYSQL_PWD=... ./ops/backup-mysql.sh /var/backups/textileconnect
#
# What it does
#   - dumps the marketplace database (and the identity database when it is a separate one) with one consistent snapshot (--single-transaction), so the shop stays online
#   - writes <dir>/<database>-<UTC time>.sql.gz and checks that the file is a complete gzip and not empty
#   - deletes dumps older than BACKUP_KEEP_DAYS (default 14)
#   - exits with an error (so cron or the scheduler can alert) if anything fails
#
# Needed: the mysql client tools (mysqldump) and a MySQL user with SELECT, SHOW VIEW, TRIGGER, EVENT, LOCK TABLES and RELOAD on the databases.
# Put the password in MYSQL_PWD or in a protected file given with --defaults-extra-file (MYSQL_DEFAULTS_FILE); never on the command line.
# Not tested against a server here (none was available when it was written): run it once by hand and restore it with restore-mysql.sh --verify before you rely on it.
set -euo pipefail

DIR="${1:?Usage: backup-mysql.sh <backup folder>}"
HOST="${MYSQL_HOST:-127.0.0.1}"
PORT="${MYSQL_PORT:-3306}"
USER_NAME="${MYSQL_USER:-root}"
DATABASES="${BACKUP_DATABASES:-textileconnect}"      # space separated; add the identity database name if it is separate
KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"

auth=()
if [[ -n "${MYSQL_DEFAULTS_FILE:-}" ]]; then auth+=("--defaults-extra-file=${MYSQL_DEFAULTS_FILE}"); fi
mkdir -p "$DIR"
chmod 700 "$DIR"
stamp="$(date -u +%Y%m%dT%H%M%SZ)"

for db in $DATABASES; do
  file="$DIR/${db}-${stamp}.sql.gz"
  tmp="${file}.partial"
  echo "Backing up ${db} to ${file}"
  mysqldump "${auth[@]}" --host="$HOST" --port="$PORT" --user="$USER_NAME" \
    --single-transaction --routines --events --triggers --hex-blob --set-gtid-purged=OFF --default-character-set=utf8mb4 \
    "$db" | gzip -9 > "$tmp"
  gzip -t "$tmp"                                    # the archive is complete
  size="$(stat -c %s "$tmp" 2>/dev/null || stat -f %z "$tmp")"
  if (( size < 1024 )); then echo "The dump of ${db} is only ${size} bytes: something is wrong." >&2; rm -f "$tmp"; exit 1; fi
  mv "$tmp" "$file"
  chmod 600 "$file"
  echo "  ok, ${size} bytes"
done

find "$DIR" -name '*.sql.gz' -type f -mtime "+${KEEP_DAYS}" -print -delete
echo "Backup finished at $(date -u +%Y-%m-%dT%H:%M:%SZ)"
