# Mobility Finance Event Platform

Mobility Finance Event Platform is a synthetic, backend-first reference system for financing and operating income-generating electric motorbikes. It demonstrates C#/.NET, event-driven microservices, Azure-oriented infrastructure, Kubernetes, observability, automated testing, and end-to-end service ownership.

Users interact with one authenticated web application. A gateway/BFF applies role-based access control and presents origination, repayments, asset operations, workflow review, and platform operations through a unified interface. The domain services and workers remain internal.

## Status

Implementation is in progress. The target system and its engineering decisions are documented in [docs/architecture.md](docs/architecture.md).

## Intended local experience

The completed repository will expose one startup command:

```bash
make app-start
```

That command will start the web application, internal .NET services, databases, Azure Service Bus emulator, device and payment simulators, OpenTelemetry collector, Prometheus, and Grafana.

## Truth boundary

This is an original portfolio project built with synthetic data. It is not affiliated with M-KOPA, does not reproduce any private platform, and does not perform real lending, payment collection, vehicle tracking, or device control.

## License

[MIT](LICENSE)
