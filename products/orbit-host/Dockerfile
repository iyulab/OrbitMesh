# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution files
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrbitMesh.Core/OrbitMesh.Core.csproj src/OrbitMesh.Core/
COPY src/OrbitMesh.Workflows/OrbitMesh.Workflows.csproj src/OrbitMesh.Workflows/
COPY src/OrbitMesh.Server/OrbitMesh.Server.csproj src/OrbitMesh.Server/
COPY src/OrbitMesh.Storage.Sqlite/OrbitMesh.Storage.Sqlite.csproj src/OrbitMesh.Storage.Sqlite/
COPY products/orbit-server/orbit-server.csproj products/orbit-server/

# Restore dependencies
RUN dotnet restore products/orbit-server/orbit-server.csproj

# Copy source code
COPY src/ src/
COPY products/orbit-server/ products/orbit-server/

# Build and publish
WORKDIR /src/products/orbit-server
RUN dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Create data directory for SQLite
RUN mkdir -p /data && chmod 777 /data

# Copy published application
COPY --from=build /app .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:5000
ENV ConnectionStrings__OrbitMesh="Data Source=/data/orbitmesh.db"
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:5000/health || exit 1

# Run the application
ENTRYPOINT ["dotnet", "orbit-server.dll"]
