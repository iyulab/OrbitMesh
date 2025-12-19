# OrbitMesh Agent Dockerfile
# Multi-stage build for optimized image size

ARG DOTNET_VERSION=10.0-preview
ARG SOURCE_REVISION_ID=local
ARG VERSION=0.1.0

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
ARG SOURCE_REVISION_ID
ARG VERSION
WORKDIR /src

# Copy solution and project files for better caching
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrbitMesh.Core/OrbitMesh.Core.csproj src/OrbitMesh.Core/
COPY src/OrbitMesh.Transport.P2P/OrbitMesh.Transport.P2P.csproj src/OrbitMesh.Transport.P2P/
COPY src/OrbitMesh.Node/OrbitMesh.Node.csproj src/OrbitMesh.Node/
COPY products/orbit-node/orbit-node.csproj products/orbit-node/

# Restore dependencies
RUN dotnet restore products/orbit-node/orbit-node.csproj -r linux-x64

# Copy all source files
COPY src/ src/
COPY products/orbit-node/ products/orbit-node/

# Build and publish
RUN dotnet publish products/orbit-node/orbit-node.csproj \
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

# Create non-root user for security
RUN groupadd -r orbitmesh && useradd -r -g orbitmesh orbitmesh

# Create data directories
RUN mkdir -p /app/data && \
    chown -R orbitmesh:orbitmesh /app

# Copy published application
COPY --from=build --chown=orbitmesh:orbitmesh /app/publish .

# Labels for GitHub Container Registry
LABEL org.opencontainers.image.source="https://github.com/iyulab/OrbitMesh"
LABEL org.opencontainers.image.description="OrbitMesh Agent - Distributed Mesh Computing Node"
LABEL org.opencontainers.image.licenses="MIT"

# Environment variables
ENV OrbitMesh__ServerUrl=http://orbitmesh-server:5000/agent
ENV OrbitMesh__AgentId=""
ENV OrbitMesh__AgentName=""
ENV OrbitMesh__Tags=""
ENV OrbitMesh__EnableShellExecution=false

# Switch to non-root user
USER orbitmesh

# Entry point
ENTRYPOINT ["dotnet", "OrbitMeshAgent.dll"]
