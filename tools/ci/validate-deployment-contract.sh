#!/usr/bin/env bash
set -euo pipefail

WORKFLOW_FILE=".github/workflows/deploy.yml"

if [[ ! -f "$WORKFLOW_FILE" ]]; then
  echo "::error file=$WORKFLOW_FILE::Deploy workflow file not found."
  exit 1
fi

publish_line="$(rg "dotnet publish ./Atlas.Api/Atlas.Api.csproj" "$WORKFLOW_FILE" || true)"

if [[ -z "$publish_line" ]]; then
  echo "::error file=$WORKFLOW_FILE::Could not find publish command for Atlas.Api."
  exit 1
fi

if [[ "$publish_line" != *"-r win-x86"* ]]; then
  echo "::error file=$WORKFLOW_FILE::Deployment contract violation: publish RID must be win-x86 for Windows App Service 32-bit Free/Shared."
  exit 1
fi

if [[ "$publish_line" != *"--self-contained true"* ]]; then
  echo "::error file=$WORKFLOW_FILE::Deployment contract violation: publish mode must remain self-contained."
  exit 1
fi

if rg -n "startup-command" "$WORKFLOW_FILE" >/dev/null; then
  echo "::error file=$WORKFLOW_FILE::Deployment contract violation: custom startup-command is not allowed without explicit contract update."
  exit 1
fi

if rg -n "win-x64|--self-contained false" "$WORKFLOW_FILE" >/dev/null; then
  echo "::error file=$WORKFLOW_FILE::Deployment contract violation: found disallowed x64/framework-dependent deployment settings."
  exit 1
fi

echo "Deployment contract validation passed."
