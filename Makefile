DOTNET_IMAGE := mcr.microsoft.com/dotnet/sdk:10.0
DOTNET := docker run --rm \
	--user "$$(id -u):$$(id -g)" \
	-e DOTNET_CLI_HOME=/tmp \
	-e NUGET_PACKAGES=/workspace/.packages \
	-v "$(CURDIR):/workspace" \
	-w /workspace \
	$(DOTNET_IMAGE) dotnet

.PHONY: restore build test format format-check check app-start app-status app-logs app-stop recovery-drill

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
