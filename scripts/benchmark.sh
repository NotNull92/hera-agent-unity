#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
CLI="$PROJECT_ROOT/hera-agent-unity"

echo "[benchmark] Building CLI..."
cd "$PROJECT_ROOT"
go build -o "$CLI" .

echo "[benchmark] Checking Unity connection..."
if ! "$CLI" status >/dev/null 2>&1; then
    echo "Error: No Unity instance found. Please open Unity Editor with the Connector package installed."
    exit 1
fi

echo "[benchmark] Checking hyperfine..."
if ! command -v hyperfine >/dev/null 2>&1; then
    echo "Error: hyperfine not found. Install: brew install hyperfine"
    exit 1
fi

RESULTS_DIR="$PROJECT_ROOT/benchmark-results"
mkdir -p "$RESULTS_DIR"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
RESULTS_FILE="$RESULTS_DIR/benchmark_${TIMESTAMP}.json"

echo "[benchmark] Running scenarios..."

hyperfine \
    --runs "${HYPERFINE_RUNS:-20}" \
    --warmup 3 \
    --export-json "$RESULTS_FILE" \
    --command-name "status (single)" \
    "$CLI status" \
    --command-name "list (single)" \
    "$CLI list" \
    --command-name "status x3 single-shot" \
    "$CLI status && $CLI status && $CLI status" \
    --command-name "list x3 single-shot" \
    "$CLI list && $CLI list && $CLI list" \
    --command-name "list x3 batch" \
    "echo '{\"commands\":[{\"command\":\"list\"},{\"command\":\"list\"},{\"command\":\"list\"}]}' | $CLI batch" \
    --command-name "editor play+stop single-shot" \
    "$CLI editor play && $CLI editor stop" \
    --command-name "editor play+stop batch" \
    "echo '{\"commands\":[{\"command\":\"manage_editor\",\"params\":{\"action\":\"play\"}},{\"command\":\"manage_editor\",\"params\":{\"action\":\"stop\"}}],\"options\":{\"fail_fast\":true}}' | $CLI batch"

echo "[benchmark] Results saved to $RESULTS_FILE"

# Print summary
echo ""
echo "=== Summary ==="
python3 -c "
import json, sys
data = json.load(open('$RESULTS_FILE'))
for r in data['results']:
    name = r['command']
    mean = r['mean']
    stddev = r['stddev']
    print(f'{name:40s}  {mean:.3f}s ± {stddev:.3f}s')
" 2>/dev/null || echo "(Install python3 for formatted summary)"
