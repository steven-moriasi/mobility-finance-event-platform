DOTNET_IMAGE := mcr.microsoft.com/dotnet/sdk:10.0
DOTNET := docker run --rm \
	--user "$$(id -u):$$(id -g)" \
	-e DOTNET_CLI_HOME=/tmp \
	-e NUGET_PACKAGES=/workspace/.packages \
	-v "$(CURDIR):/workspace" \
	-w /workspace \
	$(DOTNET_IMAGE) dotnet

.PHONY: restore build test format format-check check app-start app-status app-logs app-stop recovery-drill helm-check bicep-check workflow-check infra-check

restore:
	$(DOTNET) restore MobilityFinance.slnx

build: restore
	$(DOTNET) build MobilityFinance.slnx --configuration Release --no-restore

test: restore
	$(DOTNET) test MobilityFinance.slnx --configuration Release --no-restore

format: restore
	$(DOTNET) format MobilityFinance.slnx --no-restore

format-check: restore
	$(DOTNET) format MobilityFinance.slnx --verify-no-changes --no-restore

check: format-check build test

app-start:
	docker compose up --build --detach

app-status:
	docker compose ps

app-logs:
	docker compose logs --follow --tail=200

app-stop:
	docker compose down --remove-orphans

recovery-drill:
	./scripts/run-broker-recovery-drill.sh

helm-check:
	docker run --rm -v "$(CURDIR):/work" -w /work alpine/helm:3.18.6 \
		lint deploy/helm/mobility-finance \
		--set serviceBus.fullyQualifiedNamespace=validation.servicebus.windows.net
	docker run --rm -v "$(CURDIR):/work" -w /work alpine/helm:3.18.6 \
		template mobility deploy/helm/mobility-finance \
		--namespace mobility-finance \
		--set serviceBus.fullyQualifiedNamespace=validation.servicebus.windows.net \
		--set serviceAccount.workloadIdentity.enabled=true \
		--set serviceAccount.workloadIdentity.clientId=00000000-0000-0000-0000-000000000001 \
		--set serviceAccount.workloadIdentity.tenantId=00000000-0000-0000-0000-000000000002 \
		| docker run --rm -i ghcr.io/yannh/kubeconform:v0.7.0-alpine \
			-strict -summary

bicep-check:
	docker run --rm -v "$(CURDIR):/work" -w /work \
		mcr.microsoft.com/azure-cli:2.78.0 \
		az bicep build --file deploy/azure/main.bicep --stdout >/dev/null
	docker run --rm -v "$(CURDIR):/work" -w /work \
		mcr.microsoft.com/azure-cli:2.78.0 \
		az bicep build-params \
		--file deploy/azure/environments/dev.bicepparam --stdout >/dev/null

workflow-check:
	docker run --rm -v "$(CURDIR):/repo" -w /repo rhysd/actionlint:1.7.7

infra-check: helm-check bicep-check workflow-check
