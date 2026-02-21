#!/usr/bin/env bash
set -euo pipefail

ENV_NAME="$1"
DB_NAME="$2"
DATE_DDMMYYYY="$3"

CONTAINER="${STORAGE_CONTAINER}"
ACCOUNT="${STORAGE_ACCOUNT_NAME}"
KEY="${STORAGE_ACCOUNT_KEY}"



DATED_BLOB="${ENV_NAME}/${DB_NAME}-${DATE_DDMMYYYY}.bacpac"
LATEST_BLOB="${ENV_NAME}/${DB_NAME}-latest.bacpac"
DATED_URI="https://${ACCOUNT}.blob.core.windows.net/${CONTAINER}/${DATED_BLOB}"

echo "==> Exporting ${ENV_NAME}/${DB_NAME} to ${DATED_BLOB}"

az sql db export \
  --resource-group "${AZURE_RESOURCE_GROUP}" \
  --server "${SQL_SERVER_NAME}" \
  --name "${DB_NAME}" \
  --admin-user "${SQL_ADMIN_USER}" \
  --admin-password "${SQL_ADMIN_PASSWORD}" \
  --storage-key-type "StorageAccessKey" \
  --storage-key "${KEY}" \
  --storage-uri "${DATED_URI}"

echo "==> Copying to latest: ${LATEST_BLOB}"

az storage blob copy start \
  --account-name "${ACCOUNT}" \
  --account-key "${KEY}" \
  --destination-container "${CONTAINER}" \
  --destination-blob "${LATEST_BLOB}" \
  --source-uri "${DATED_URI}" \
  --output none

echo "==> Retention: keep last 5 dated backups for ${ENV_NAME}/${DB_NAME}"

BLOBS_JSON="$(az storage blob list \
  --account-name "${ACCOUNT}" \
  --account-key "${KEY}" \
  --container-name "${CONTAINER}" \
  --prefix "${ENV_NAME}/${DB_NAME}-" \
  --query "[].name" -o json)"

DATED_BLOBS="$(echo "${BLOBS_JSON}" | jq -r '.[]' | grep -E "^${ENV_NAME}/${DB_NAME}-[0-9]{2}-[0-9]{2}-[0-9]{4}\.bacpac$" || true)"

if [[ -z "${DATED_BLOBS}" ]]; then
  echo "No dated blobs found. Skipping retention."
  exit 0
fi

TO_DELETE="$(echo "${DATED_BLOBS}" | while read -r n; do
    base="$(basename "$n")"
    datepart="$(echo "$base" | sed -E "s/^${DB_NAME}-([0-9]{2}-[0-9]{2}-[0-9]{4})\.bacpac$/\1/")"
    dd="${datepart:0:2}"
    mm="${datepart:3:2}"
    yyyy="${datepart:6:4}"
    sortkey="${yyyy}-${mm}-${dd}"
    echo "${sortkey} ${n}"
  done | sort -r | awk 'NR>5 {print $2}')"

if [[ -n "${TO_DELETE}" ]]; then
  echo "Deleting old backups:"
  echo "${TO_DELETE}"
  while read -r blob; do
    az storage blob delete \
      --account-name "${ACCOUNT}" \
      --account-key "${KEY}" \
      --container-name "${CONTAINER}" \
      --name "${blob}" \
      --output none
  done <<< "${TO_DELETE}"
else
  echo "Nothing to delete (<=5 backups)."
fi

echo "==> Done for ${ENV_NAME}/${DB_NAME}"
