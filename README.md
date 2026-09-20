# Mobility Finance Event Platform

Mobility Finance Event Platform is a synthetic, backend-first reference system for financing and operating income-generating electric motorbikes. It demonstrates C#/.NET, event-driven microservices, Azure-oriented infrastructure, Kubernetes, observability, automated testing, and end-to-end service ownership.

Users interact with one authenticated web application. A gateway/BFF applies role-based access control and presents origination, repayments, asset operations, workflow review, and platform operations through a unified interface. The domain services and workers remain internal.

## Status

Implementation is in progress. The target system and its engineering decisions are documented in [docs/architecture.md](docs/architecture.md).

## Local experience

Start the current web application, internal APIs, and workers:

```bash
make app-start
```

Open `http://localhost:8080` and sign in with one of the synthetic local accounts:

| Role | Email | Password |
| --- | --- | --- |
| Operator | `operator@mobility.local` | `Operator!2026` |
| Reviewer | `reviewer@mobility.local` | `Reviewer!2026` |
| Platform administrator | `admin@mobility.local` | `Admin!2026` |

The role-aware shell exposes only the workspaces allowed for the signed-in account, while the BFF applies the same authorization policies server-side. Future increments add the databases, Azure Service Bus emulator, simulators, OpenTelemetry collector, Prometheus, and Grafana behind the same startup command.

## Truth boundary

This is an original portfolio project built with synthetic data. It is not affiliated with M-KOPA, does not reproduce any private platform, and does not perform real lending, payment collection, vehicle tracking, or device control.

## License

[MIT](LICENSE)
