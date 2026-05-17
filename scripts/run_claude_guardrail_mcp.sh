#!/bin/zsh

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
PORT="${GUARDRAIL_LOCAL_PORT:-5099}"
BASE_URL="http://127.0.0.1:${PORT}"
MANIFEST_URL="${BASE_URL}/.well-known/ai-guardrail.json"
MCP_URL="${BASE_URL}/mcp"
LOG_FILE="${TMPDIR:-/tmp}/guardrail-claude-mcp.log"
API_PID=""

cleanup() {
  if [[ -n "${API_PID}" ]]; then
    kill "${API_PID}" 2>/dev/null || true
    wait "${API_PID}" 2>/dev/null || true
  fi
}

trap cleanup EXIT INT TERM

echo "Starting Guardrail API for Claude Desktop on ${BASE_URL}" >&2

ASPNETCORE_ENVIRONMENT=Development \
Auth__DisableAuth=true \
ConnectionStrings__PostgreSql= \
Guardrail__ApplyDatabaseOnStartup=true \
Guardrail__SeedDataOnStartup=true \
Guardrail__PolicySeedPath="${ROOT_DIR}/policies/samples" \
Guardrail__EvaluationSeedPath="${ROOT_DIR}/evaluations/datasets" \
dotnet run --project "${ROOT_DIR}/src/Guardrail.API" --urls "${BASE_URL}" >>"${LOG_FILE}" 2>&1 &
API_PID=$!

for _ in {1..60}; do
  if curl -fsS "${MANIFEST_URL}" >/dev/null 2>&1; then
    break
  fi
  sleep 1
done

if ! curl -fsS "${MANIFEST_URL}" >/dev/null 2>&1; then
  echo "Guardrail API did not become ready. Recent log output:" >&2
  tail -n 80 "${LOG_FILE}" >&2 || true
  exit 1
fi

echo "Guardrail API is ready. Bridging stdio to ${MCP_URL}" >&2
exec npx mcp-remote "${MCP_URL}"
