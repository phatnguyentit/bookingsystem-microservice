#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Load base defaults from .env (committed, no real secrets)
if [[ -f "$SCRIPT_DIR/.env" ]]; then
    source "$SCRIPT_DIR/.env"
fi

# Override with .env.dev when present (gitignored, holds real secrets)
if [[ -f "$SCRIPT_DIR/.env.dev" ]]; then
    source "$SCRIPT_DIR/.env.dev"
fi

: "${POSTMAN_API_KEY:?POSTMAN_API_KEY is not set. Copy .env.dev to .env and fill it in.}"
: "${COLLECTION_ID:?COLLECTION_ID is not set. Copy .env.dev to .env and fill it in.}"

# Accept base URL as first argument, e.g.:  ./run-postman.sh http://localhost:12345
# Falls back to the port saved in the collection variable.
BASE_URL="${1:-}"

postman login --with-api-key "$POSTMAN_API_KEY"

if [[ -n "$BASE_URL" ]]; then
    postman collection run "$COLLECTION_ID" --env-var "baseUrl=$BASE_URL"
else
    postman collection run "$COLLECTION_ID"
fi
