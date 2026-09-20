# Deployment and Delivery

The repository contains deployable Kubernetes and Azure definitions, but it
does not claim that a public cloud environment currently exists. Local
compilation and schema validation prove that the artifacts are structurally
valid; an Azure deployment is only considered proven after a subscription
deployment, rollout, and user-journey verification complete.

## Artifact map

- `deploy/docker/Dockerfile` builds every .NET API, web application, and
  worker from an explicit project path and assembly name.
- `deploy/helm/mobility-finance` packages the seven runtime components with
  health probes, resource boundaries, rolling updates, topology spreading,
  disruption budgets, autoscaling, network policies, and Azure workload
  identity.
- `deploy/azure/main.bicep` composes networking, AKS, ACR, Service Bus,
  PostgreSQL, Key Vault, Log Analytics, managed Prometheus, and managed
  Grafana modules.
- `.github/workflows` validates code and infrastructure, scans the repository,
  publishes attested release images, and provides a manually gated Azure
  deployment.

## Local validation

Run the application quality suite and infrastructure validation without an
Azure account:

```bash
make check
make infra-check
```

The infrastructure check:

1. lints and renders the Helm chart;
2. validates every rendered Kubernetes resource against strict schemas;
3. compiles the Bicep entry point and development parameter file; and
4. validates every GitHub Actions workflow with `actionlint`.

No credentials are read or written by these commands.

## Azure provisioning

The Bicep entry point targets an existing resource group. Replace the
placeholder Microsoft Entra administrator values in the environment parameter
file, then perform a what-if review before deployment:

```bash
az deployment group what-if \
  --resource-group <resource-group> \
  --template-file deploy/azure/main.bicep \
  --parameters deploy/azure/environments/dev.bicepparam

az deployment group create \
  --resource-group <resource-group> \
  --template-file deploy/azure/main.bicep \
  --parameters deploy/azure/environments/dev.bicepparam
```

The application identity receives only Service Bus sender/receiver and Key
Vault secrets-user roles. AKS receives ACR pull access. Service Bus local
authentication and the ACR admin user are disabled.

The development parameter file leaves the AKS API public so a hosted delivery
runner can reach it. Production should retain the `privateCluster` default and
use a self-hosted runner with private network access.

## GitHub environment contract

The manual `Deploy to Azure` workflow uses OpenID Connect and a protected
GitHub Environment. Configure these environment variables:

| Variable | Purpose |
| --- | --- |
| `AZURE_CLIENT_ID` | Federated deployment identity application/client ID |
| `AZURE_TENANT_ID` | Microsoft Entra tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Target Azure subscription |
| `AZURE_RESOURCE_GROUP` | Provisioned resource group |
| `AZURE_CONTAINER_REGISTRY` | ACR resource name |
| `AZURE_AKS_CLUSTER` | AKS cluster name |

Configure required reviewers on the production GitHub Environment. The
workflow accepts an immutable image tag, builds all seven images in ACR,
performs an atomic Helm upgrade, and waits for every deployment rollout.

## Release behavior

Tags matching `v*` publish versioned and commit-addressed images to GitHub
Container Registry. Each image receives a build-provenance attestation. The
Helm chart is packaged as a release artifact using the tag version.

Cloud rollout, availability, and production SLO claims remain unverified until
the Azure deployment workflow completes in a real subscription and the
authenticated web journeys and recovery exercises are rerun there.
