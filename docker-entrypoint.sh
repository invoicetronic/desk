#!/bin/sh
# docker-entrypoint.sh — fix ownership on writable mount points and drop privileges.
#
# The container starts as root just long enough to chown the host-bind-mounted
# directories that the app needs to write to (logs and SQLite data). Without this,
# bind-mounting a host directory with a different UID would prevent the non-root
# `app` user (UID 1654) from writing — leading to a startup crash.
#
# Once permissions are fixed, the script execs the actual command as `app`.

set -e

if [ "$(id -u)" = "0" ]; then
    for dir in /app/logs /app/data; do
        if [ -d "$dir" ]; then
            chown -R app:app "$dir" 2>/dev/null || true
        fi
    done
    exec setpriv --reuid=app --regid=app --init-groups -- "$@"
fi

exec "$@"
