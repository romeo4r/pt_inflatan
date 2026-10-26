# syntax=docker/dockerfile:1
# Multi-stage build: SDK image compiles and publishes, the runtime image only ships the output.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so dependency layers are cached while source changes.
COPY Mundialito.slnx Directory.Build.props global.json ./
COPY src/Mundialito.Domain/Mundialito.Domain.csproj src/Mundialito.Domain/
COPY src/Mundialito.Application/Mundialito.Application.csproj src/Mundialito.Application/
COPY src/Mundialito.Infrastructure/Mundialito.Infrastructure.csproj src/Mundialito.Infrastructure/
COPY src/Mundialito.Api/Mundialito.Api.csproj src/Mundialito.Api/
RUN dotnet restore src/Mundialito.Api/Mundialito.Api.csproj

COPY src/ src/
RUN dotnet publish src/Mundialito.Api/Mundialito.Api.csproj -c Release -o /app/publish --no-restore

# The full (non-Alpine) runtime image is required: Microsoft.Data.SqlClient needs ICU.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is only used by the container health check.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --start-period=40s --retries=6 CMD curl -fsS http://localhost:8080/health || exit 1

USER app
ENTRYPOINT ["dotnet", "Mundialito.Api.dll"]
