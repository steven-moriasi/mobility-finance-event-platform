#!/usr/bin/env bash

set -euo pipefail

project_name="$(docker compose config --format json | jq -r '.name')"
network_name="${project_name}_default"
applicant_reference="SYNTH-RECOVERY-$(date +%s)"

api_request() {
  docker run --rm \
    --network "${network_name}" \
    curlimages/curl:8.16.0 \
    -fsS \
    "$@"
}

restore_broker() {
  docker compose start servicebus-emulator >/dev/null 2>&1 || true
}

trap restore_broker EXIT

docker compose ps --status running --services | rg -q '^origination-api$'
docker compose ps --status running --services | rg -q '^activation-worker$'
docker compose ps --status running --services | rg -q '^servicebus-emulator$'

application="$(
  api_request \
    -H 'Content-Type: application/json' \
    -d "{
      \"applicantReference\":\"${applicant_reference}\",
      \"market\":\"KE\",
      \"currency\":\"KES\",
      \"assetPrice\":250000,
      \"deposit\":50000,
      \"termWeeks\":52,
      \"weeklyIncome\":20000
    }" \
    http://origination-api:8080/applications/
)"
application_id="$(jq -er '.id' <<<"${application}")"
offer_id="$(jq -er '.offer.id' <<<"${application}")"

docker compose stop servicebus-emulator >/dev/null

agreement="$(
  api_request \
    -H 'Content-Type: application/json' \
    -d "{\"offerId\":\"${offer_id}\"}" \
    "http://origination-api:8080/applications/${application_id}/accept"
)"
agreement_id="$(jq -er '.id' <<<"${agreement}")"

docker compose start servicebus-emulator >/dev/null

for _ in $(seq 1 90); do
  workflow="$(
    api_request \
      "http://activation-worker:8080/workflows/${agreement_id}" \
      2>/dev/null \
      || true
  )"

  if [[ -n "${workflow}" ]] \
    && jq -e '.state == "AwaitingDeposit"' <<<"${workflow}" >/dev/null 2>&1
  then
    workflow_state="$(jq -er '.state' <<<"${workflow}")"
    jq -n \
      --arg applicationId "${application_id}" \
      --arg agreementId "${agreement_id}" \
      --arg state "${workflow_state}" \
      '{
        drill: "service-bus-outage-recovery",
        applicationId: $applicationId,
        agreementId: $agreementId,
        recoveredWorkflowState: $state,
        result: "passed"
      }'
    exit 0
  fi

  sleep 2
done

printf 'Agreement event was not delivered after broker recovery.\n' >&2
exit 1
