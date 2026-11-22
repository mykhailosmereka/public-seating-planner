#!/usr/bin/env bash
# Runs built test executables inside the Claude Code sandbox against an external PostgreSQL.
#
# The Linux sandbox blocks Unix sockets (no Docker, no `dotnet test`) and direct connections to
# host ports. Its network proxy can still tunnel TCP to a host:port listed in
# sandbox.network.allowedDomains, so this script forwards a local port through that proxy with socat.
# Outside the sandbox (no HTTPS_PROXY) it connects directly.
#
# Usage: SEATINGPLANNER_TEST_POSTGRES='Host=127.0.0.1;Port=54329;...' tools/run-tests-sandboxed.sh [test-project-dir...]
# Build first (`dotnet build`); defaults to every project under tests/.
set -euo pipefail

: "${SEATINGPLANNER_TEST_POSTGRES:?Set SEATINGPLANNER_TEST_POSTGRES to the test server connection string}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
configuration="${CONFIGURATION:-Debug}"
framework="${TARGET_FRAMEWORK:-net10.0}"

if [[ $# -eq 0 ]]; then
  set -- "$repo_root"/tests/*/
fi

conn="$SEATINGPLANNER_TEST_POSTGRES"
host="$(grep -oiP '(?<=(^|;)host=)[^;]+' <<<"$conn" || true)"
port="$(grep -oiP '(?<=(^|;)port=)[^;]+' <<<"$conn" || echo 5432)"
[[ -n "$host" ]] || { echo "Connection string has no Host=" >&2; exit 1; }

socat_pid=""
cleanup() { [[ -n "$socat_pid" ]] && kill "$socat_pid" 2>/dev/null || true; }
trap cleanup EXIT

if [[ -n "${HTTPS_PROXY:-}" ]]; then
  command -v socat >/dev/null || { echo "socat is required inside the sandbox" >&2; exit 1; }

  # http://user:password@proxyhost:proxyport
  if [[ ! "$HTTPS_PROXY" =~ ^https?://([^:@/]+):([^@/]+)@([^:/]+):([0-9]+) ]]; then
    echo "Unsupported HTTPS_PROXY format" >&2
    exit 1
  fi
  proxy_auth="${BASH_REMATCH[1]}:${BASH_REMATCH[2]}"
  proxy_host="${BASH_REMATCH[3]}"
  proxy_port="${BASH_REMATCH[4]}"

  local_port="$(python3 -I -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1])')"

  socat "TCP-LISTEN:${local_port},bind=127.0.0.1,reuseaddr,fork" \
    "PROXY:${proxy_host}:${host}:${port},proxyport=${proxy_port},proxyauth=${proxy_auth}" &
  socat_pid=$!

  for _ in $(seq 50); do
    (exec 3<>"/dev/tcp/127.0.0.1/${local_port}") 2>/dev/null && break
    sleep 0.1
  done

  conn="$(sed -E "s/(^|;)[Hh]ost=[^;]+/\1Host=127.0.0.1/; s/(^|;)[Pp]ort=[^;]+/\1Port=${local_port}/" <<<"$conn")"
  [[ "$conn" =~ (^|;)[Pp]ort= ]] || conn="${conn};Port=${local_port}"
fi

status=0
for project_dir in "$@"; do
  project_dir="${project_dir%/}"
  name="$(basename "$project_dir")"
  exe="$project_dir/bin/$configuration/$framework/$name"
  [[ -x "$exe" ]] || { echo "Not built: $exe (run dotnet build first)" >&2; status=1; continue; }

  SEATINGPLANNER_TEST_POSTGRES="$conn" "$exe" || status=$?
done

exit "$status"
