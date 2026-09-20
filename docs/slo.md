# Service-level objectives

These values are design targets for the synthetic platform. They are not
presented as production measurements. Local measurements are retained only
when the workload, hardware, duration, and result are recorded together.

## User-facing objectives

| Journey | Indicator | Target |
| --- | --- | --- |
| Web application availability | Successful non-static HTTP requests | 99.9% per 30 days |
| Submit and price application | P95 server duration | Under 500 ms |
| Record repayment | P95 server duration | Under 750 ms |
| Load operator workspace | P95 server duration | Under 1 second |

## Event-processing objectives

| Journey | Indicator | Target |
| --- | --- | --- |
| Agreement event delivery | P95 age from outbox enqueue to workflow receipt | Under 10 seconds |
| Payment projection freshness | P95 event age at projection update | Under 30 seconds |
| Activation completion | P95 from final prerequisite to activation | Under 15 seconds |
| Device heartbeat freshness | P95 time since last assigned-asset heartbeat | Under 5 minutes |
| Dead-letter containment | Time from first dead letter to alert | Under 5 minutes |

## Correctness objectives

- no unbalanced ledger transaction is committed;
- no message ID mutates an aggregate more than once;
- no asset is assigned to two agreements;
- no workflow activates before deposit and asset prerequisites;
- no sensitive command is accepted without platform-administrator
  authorization;
- no replay occurs without an audit reason and reviewer identity.

Correctness objectives are release-blocking invariants rather than error-budget
targets.

## Error-budget response

1. Freeze non-reliability changes when a monthly availability budget is
   exhausted.
2. Identify the failing service and correlated trace.
3. Contain poison messages before replaying them.
4. Prefer rollback over forward-fixing an active customer-impacting release.
5. Record the incident, contributing conditions, recovery evidence, and
   follow-up owner.

## Dashboard signals

The local Grafana dashboard uses OpenTelemetry-derived Prometheus metrics for
request rate, P95 latency, and .NET runtime health. Future service-specific
meters add message age, retries, workflow completion, heartbeat freshness, and
ledger invariant failures.
