#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TEST_PROJECT="${REPO_ROOT}/Atlas.Api.Tests/Atlas.Api.Tests.csproj"
RESULTS_DIR="${REPO_ROOT}/Atlas.Api.Tests/TestResults"
REPORT_DIR="${REPO_ROOT}/tools/coverage/report"

rm -rf "${RESULTS_DIR}" "${REPORT_DIR}"

pushd "${REPO_ROOT}" >/dev/null

dotnet test "${TEST_PROJECT}" \
  --configuration Release \
  --collect:"XPlat Code Coverage" \
  --settings "${REPO_ROOT}/coverage.runsettings"

coverage_file="$(find "${RESULTS_DIR}" -name 'coverage.cobertura.xml' -print | head -n 1)"
if [[ -z "${coverage_file}" ]]; then
  echo "coverage.cobertura.xml was not generated." >&2
  exit 1
fi

reportgenerator_path="${REPO_ROOT}/.tools/reportgenerator"
if [[ ! -x "${reportgenerator_path}" ]]; then
  dotnet tool install --tool-path "${REPO_ROOT}/.tools" dotnet-reportgenerator-globaltool
fi

"${reportgenerator_path}" \
  -reports:"${RESULTS_DIR}/**/coverage.cobertura.xml" \
  -targetdir:"${REPORT_DIR}" \
  -reporttypes:"Html;TextSummary"

echo "Coverage report: ${REPORT_DIR}/index.html"
echo "Coverage summary: ${REPORT_DIR}/Summary.txt"

popd >/dev/null
