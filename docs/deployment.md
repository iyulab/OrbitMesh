# OrbitMesh Docker Deployment Guide

This guide covers deploying OrbitMesh using Docker Compose with automatic updates.

## Quick Start

### 1. Pull and Run

```bash
# Create project directory
mkdir orbitmesh && cd orbitmesh

# Download docker-compose.yml
curl -O https://raw.githubusercontent.com/iyulab/OrbitMesh/main/docker-compose.yml

# Create config directory
mkdir -p config

# Start services
docker compose up -d
```

### 2. Set Admin Password

Choose one method:

**Option A: Environment Variable (Recommended for Docker)**
```bash
# Create .env file
echo "ORBITMESH_ADMIN_PASSWORD=your-secure-password" > .env

# Or set directly in docker-compose.yml
environment:
  - ORBITMESH_ADMIN_PASSWORD=your-secure-password
```

**Option B: Configuration File**
```bash
# Create config/appsettings.json
cat > config/appsettings.json << 'EOF'
{
  "OrbitMesh": {
    "AdminPassword": "your-secure-password"
  }
}
EOF
```

### 3. Access the Dashboard

Open http://localhost:5000 in your browser.

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Docker Host                          │
├─────────────────────────────────────────────────────────┤
│  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │   Watchtower    │  │     orbitmesh-server        │  │
│  │  (Auto-update)  │  │  ┌─────────────────────┐    │  │
│  │                 │  │  │   Port 5000         │    │  │
│  │ Checks ghcr.io  │──│  │   SignalR Hub       │    │  │
│  │ every 5 min     │  │  │   REST API          │    │  │
│  └─────────────────┘  │  │   Web Dashboard     │    │  │
│                       │  └─────────────────────┘    │  │
│                       │  Volume: orbitmesh-data     │  │
│                       └─────────────────────────────┘  │
│                                     │                   │
│                       ┌─────────────┴─────────────┐    │
│                       ▼                           ▼    │
│  ┌─────────────────────────┐  ┌─────────────────────────┐
│  │   orbitmesh-agent-1     │  │   orbitmesh-agent-2     │
│  │   (Worker Node)         │  │   (Worker Node)         │
│  └─────────────────────────┘  └─────────────────────────┘
└─────────────────────────────────────────────────────────┘
```

## Configuration Options

### Server Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ORBITMESH_ADMIN_PASSWORD` | Admin console password | (none) |
| `ASPNETCORE_ENVIRONMENT` | Environment name | Production |
| `ConnectionStrings__OrbitMesh` | SQLite database path | /app/data/orbitmesh.db |

### Agent Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `OrbitMesh__ServerUrl` | Server SignalR endpoint | http://orbitmesh-server:5000/agent |
| `OrbitMesh__AgentName` | Display name for agent | (hostname) |
| `OrbitMesh__Tags` | Comma-separated tags | (none) |
| `OrbitMesh__EnableShellExecution` | Allow shell command execution | false |

## Automatic Updates with Watchtower

Watchtower automatically checks for new images and updates containers.

### How It Works

1. Watchtower polls GitHub Container Registry every 5 minutes
2. When a new image is detected, it:
   - Pulls the new image
   - Stops the running container
   - Starts a new container with the same configuration
   - Removes the old image

### Update Behavior

- **Zero-downtime updates**: Agents reconnect automatically after server restarts
- **Configuration preserved**: Volume mounts ensure data persistence
- **Rollback**: Use Docker to manually rollback if needed

### Check Current Version

```bash
# API endpoint (no authentication required)
curl http://localhost:5000/api/version

# Response example:
{
  "version": "0.1.0",
  "fullVersion": "0.1.0+abc1234",
  "gitCommit": "abc1234",
  "buildDate": "2025-01-15T10:30:00Z",
  "runtime": ".NET 10.0.0",
  "os": "Linux 5.15.0 (X64)",
  "product": "OrbitMesh Server"
}
```

### Disable Auto-Update for Specific Containers

Remove or modify the Watchtower label:
```yaml
labels:
  - "com.centurylinklabs.watchtower.enable=false"
```

### Manual Update

```bash
# Pull latest images
docker compose pull

# Recreate containers
docker compose up -d
```

## Scaling Agents

### Add More Agents

```yaml
# docker-compose.override.yml
services:
  orbitmesh-agent-2:
    image: ghcr.io/iyulab/orbitmesh-agent:latest
    environment:
      - OrbitMesh__ServerUrl=http://orbitmesh-server:5000/agent
      - OrbitMesh__AgentName=agent-2
      - OrbitMesh__Tags=docker,worker
    depends_on:
      orbitmesh-server:
        condition: service_healthy
    networks:
      - orbitmesh

  orbitmesh-agent-3:
    image: ghcr.io/iyulab/orbitmesh-agent:latest
    environment:
      - OrbitMesh__ServerUrl=http://orbitmesh-server:5000/agent
      - OrbitMesh__AgentName=agent-3
      - OrbitMesh__Tags=docker,gpu
    depends_on:
      orbitmesh-server:
        condition: service_healthy
    networks:
      - orbitmesh
```

### Using Docker Compose Scale

```bash
# Start 5 agent instances
docker compose up -d --scale orbitmesh-agent=5
```

## Data Persistence

### Volumes

| Volume | Path | Purpose |
|--------|------|---------|
| `orbitmesh-data` | /app/data | SQLite database, logs |

### Backup

```bash
# Stop server for consistent backup
docker compose stop orbitmesh-server

# Backup volume
docker run --rm -v orbitmesh-data:/data -v $(pwd):/backup alpine \
  tar czf /backup/orbitmesh-backup-$(date +%Y%m%d).tar.gz -C /data .

# Restart server
docker compose start orbitmesh-server
```

### Restore

```bash
docker compose down
docker volume rm orbitmesh_orbitmesh-data
docker volume create orbitmesh_orbitmesh-data
docker run --rm -v orbitmesh_orbitmesh-data:/data -v $(pwd):/backup alpine \
  tar xzf /backup/orbitmesh-backup-YYYYMMDD.tar.gz -C /data
docker compose up -d
```

## Security Considerations

### Production Checklist

- [ ] Set strong admin password
- [ ] Use HTTPS with reverse proxy (nginx, Traefik, Caddy)
- [ ] Restrict network access to admin endpoints
- [ ] Disable shell execution on agents unless required
- [ ] Regular backups
- [ ] Monitor container logs

### Reverse Proxy Example (Traefik)

```yaml
# docker-compose.override.yml
services:
  orbitmesh-server:
    labels:
      - "traefik.enable=true"
      - "traefik.http.routers.orbitmesh.rule=Host(`orbitmesh.example.com`)"
      - "traefik.http.routers.orbitmesh.tls.certresolver=letsencrypt"
      - "traefik.http.services.orbitmesh.loadbalancer.server.port=5000"
```

## Troubleshooting

### Server Won't Start

```bash
# Check logs
docker compose logs orbitmesh-server

# Common issues:
# - Port 5000 already in use
# - Database permission issues
# - Invalid configuration
```

### Agents Won't Connect

```bash
# Check agent logs
docker compose logs orbitmesh-agent

# Common issues:
# - Wrong server URL
# - Network not accessible
# - Server not healthy yet (wait for health check)
```

### Watchtower Not Updating

```bash
# Check Watchtower logs
docker compose logs watchtower

# Force update check
docker exec watchtower /watchtower --run-once
```

## Development Setup

For local development with hot reload:

```bash
# Use development compose file
docker compose -f docker-compose.dev.yml up --build
```

This builds from source instead of using pre-built images.
