FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG PROJECT_PATH
ARG APP_DLL

WORKDIR /source
COPY . .

RUN dotnet restore "${PROJECT_PATH}"
RUN dotnet publish "${PROJECT_PATH}" \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

ARG APP_DLL

ENV APP_DLL="${APP_DLL}" \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY --from=build /app/publish .

USER app
EXPOSE 8080

ENTRYPOINT ["sh", "-c", "exec dotnet \"$APP_DLL\""]
