# Threat model

## Scope and trust boundaries

The protected assets are financing decisions, ledger integrity, asset
assignments, device commands, authentication sessions, integration events, and
operational telemetry.

The main trust boundaries are:

1. browser to the authenticated web application and BFF;
2. BFF to internal service APIs;
3. services and workers to Azure Service Bus;
4. workloads to their service-owned data stores;
5. workloads to the OpenTelemetry collector;
6. deployment automation to Azure control-plane APIs.

The local environment uses synthetic identities and data. Production-oriented
Azure definitions use managed identity, private networking, Key Vault, and
service-specific authorization instead of shared local credentials.

## Threats and controls

| Threat | Impact | Current control | Cloud control |
| --- | --- | --- | --- |
| Forged browser mutation | Unauthorized financing, payment, or fleet action | Antiforgery tokens, same-site cookies, server-side policy checks | Same controls behind TLS and an application gateway |
| Credential guessing | Account compromise | Fixed-window login rate limit, generic failure response, no credential logging | External OIDC, conditional access, managed lockout |
| Privilege escalation | Access to review or platform actions | Role claims and named authorization policies applied to pages and BFF endpoints | Identity-provider group mapping and least-privilege app roles |
| Session theft | Account impersonation | HTTP-only, strict same-site cookies; secure host cookie outside development | TLS-only ingress, short sessions, centralized revocation |
| Cross-site scripting | Session and data exposure | Razor encoding and restrictive response headers; no user-supplied HTML | Content Security Policy reporting and managed edge rules |
| Duplicate or replayed event | Duplicate activation or ledger mutation | Stable message IDs, inbox-style deduplication, aggregate sequence | Persistent inbox with unique message constraint |
| Out-of-order event | Invalid workflow transition | Service Bus sessions and workflow state validation | Session-aware consumers and optimistic concurrency |
| Poisoned event | Consumer failure loop | Bounded delivery attempts and dead-lettering for invalid envelopes | Alerted dead-letter queues and audited replay |
| Broker outage | Delayed workflow progress | Bounded outbox retry and an executable outage recovery drill | Durable transactional outbox and availability alerts |
| Forged provider callback | False payment state | Provider transaction deduplication; public provider simulator remains synthetic | Signed callback validation, timestamp and nonce checks |
| Unauthorized device command | Asset misuse | Administrator policy and immutable command history | Dual approval, device identity, command expiry, signed payloads |
| Telemetry data leakage | Location or customer exposure | Synthetic coordinates and business-safe structured logging | Attribute allowlists, retention limits, private collector endpoint |
| Secret disclosure | Credential compromise | No secrets in source; local emulator credentials are non-production | Key Vault references and workload identity |
| Dependency compromise | Build or runtime compromise | Pinned dependency versions and reviewed updates | Dependency, container, and provenance scanning in delivery |

## Residual risks

- Local accounts are intentionally demonstrative and must not be used outside
  the development environment.
- Current repositories and inboxes are in-memory; container restart recovery
  is not claimed until durable PostgreSQL persistence is implemented.
- The local Service Bus emulator does not reproduce every Azure behavior.
- The simulator does not represent a production payment provider or vehicle
  command channel.
- Grafana anonymous viewer access is local-only and must not be used for an
  internet-facing deployment.

## Security verification

Verification includes:

- authorization policy unit tests;
- antiforgery enforcement on every browser mutation;
- runtime authentication throttling;
- response-header inspection;
- duplicate payment and event tests;
- domain invariants for ledger and asset operations;
- secret, dependency, container, and infrastructure scans in continuous
  delivery;
- explicit recovery and replay exercises.
