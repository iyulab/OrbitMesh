# OrbitMesh Server Dockerfile
# Multi-stage build for optimized image size

ARG DOTNET_VERSION=10.0-preview
ARG SOURCE_REVISION_ID=local
ARG VERSION=0.1.0

# Build frontend
FROM node:22-alpine AS frontend
WORKDIR /app
COPY products/orbit-host/ClientApp/package*.json ./
RUN npm ci
COPY products/orbit-host/ClientApp/ ./
RUN npm run build

# Build .NET application
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG SOURCE_REVISION_ID
ARG VERSION
WORKDIR /src

# Copy solution and project files for better caching
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrbitMesh.Core/OrbitMesh.Core.csproj src/OrbitMesh.Core/
COPY src/OrbitMesh.Host/OrbitMesh.Host.csproj src/OrbitMesh.Host/
COPY src/OrbitMesh.Workflows/OrbitMesh.Workflows.csproj src/OrbitMesh.Workflows/
COPY src/OrbitMesh.Storage.Sqlite/OrbitMesh.Storage.Sqlite.csproj src/OrbitMesh.Storage.Sqlite/
COPY products/orbit-host/orbit-host.csproj products/orbit-host/

# Restore dependencies
RUN dotnet restore products/orbit-host/orbit-host.csproj -r linux-x64

# Copy all source files
COPY src/ src/
COPY products/orbit-host/ products/orbit-host/

# Copy frontend build output
COPY --from=frontend /app/../wwwroot products/orbit-host/wwwroot/

# Build and publish
RUN dotnet publish products/orbit-host/orbit-host.csproj \
    -c Release \
    -o /app/publish \
    -r linux-x64 \
    --no-restore \
    -p:PublishSingleFile=false \
    -p:SelfContained=false \
    -p:SourceRevisionId=${SOURCE_REVISION_ID} \
    -p:Version=${VERSION} \
    -p:TreatWarningsAsErrors=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y --no-install-recommends curl && \
    rm -rf /var/lib/apt/lists/*

# Create non-root user for security
RUN groupadd -r orbitmesh && useradd -r -g orbitmesh orbitmesh

# Create data directories
RUN mkdir -p /app/data /app/logs && \
    chown -R orbitmesh:orbitmesh /app

# Copy published application
COPY --from=build --chown=orbitmesh:orbitmesh /app/publish .

# Labels for GitHub Container Registry
LABEL org.opencontainers.image.source="https://github.com/iyulab/OrbitMesh"
LABEL org.opencontainers.image.description="OrbitMesh Server - Distributed Mesh Computing Framework"
LABEL org.opencontainers.image.licenses="MIT"

# Environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ConnectionStrings__OrbitMesh="Data Source=/app/data/orbitmesh.db"

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Expose port
EXPOSE 5000

# Switch to non-root user
USER orbitmesh

# Entry point
ENTRYPOINT ["dotnet", "OrbitMeshServer.dll"]
