# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution files
COPY Directory.Build.props Directory.Packages.props ./
COPY src/OrbitMesh.Core/OrbitMesh.Core.csproj src/OrbitMesh.Core/
COPY src/OrbitMesh.Agent/OrbitMesh.Agent.csproj src/OrbitMesh.Agent/
COPY products/orbit-agent/orbit-agent.csproj products/orbit-agent/

# Restore dependencies
RUN dotnet restore products/orbit-agent/orbit-agent.csproj

# Copy source code
COPY src/ src/
COPY products/orbit-agent/ products/orbit-agent/

# Build and publish
WORKDIR /src/products/orbit-agent
RUN dotnet publish -c Release -o /app --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published application
COPY --from=build /app .

# Set environment variables
ENV OrbitMesh__ServerUrl=http://orbit-server:5000/agent
ENV OrbitMesh__EnableShellExecution=false

# Run the application
ENTRYPOINT ["dotnet", "orbit-agent.dll"]
