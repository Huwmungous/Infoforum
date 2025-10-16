#!/usr/bin/env bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUT="${SCRIPT_DIR}/publish"
dotnet publish "${SCRIPT_DIR}/ConfigManagementMcpServer.csproj" -c Release -o "${OUT}"
echo "Published ConfigManagementMcpServer webservice to $OUT"
