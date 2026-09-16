# Setting Up FastMCP: Local Environment and Containerization

This guide details how to configure, run, and connect the **Comfy MCP Gateway** and Fleet Tool Surface across local workstations and containerized environments.

---

## 1. Architecture Overview

```
+-----------------------------------------------------------------------------+
|                                MCP CLIENTS                                  |
|   Antigravity (AGY)  |  Claude Desktop / Cursor  |  Autonomous Orchestrator |
+-----------------------------------------------------------------------------+
                                       │
                         JSON-RPC (stdio or HTTP/SSE)
                                       ▼
+-----------------------------------------------------------------------------+
|                         COMFY FAST-MCP GATEWAY                              |
|                          (c:\work\isolate)                                  |
|                                                                             |
|  * Port 8720 (direct source) / Port 8722 (Docker Compose loopback)          |
|  * Tools: fleet_mod_inventory, fleet_conflict_audit, fleet_swap_profile     |
|  * Tailscale & SSH Bridge: OMEN (Local) | AM4 | FX99 | i5                   |
+-----------------------------------------------------------------------------+
```

---

## 2. Option A: Local Python Setup (Recommended for Development)

The gateway runs natively on Windows (OMEN) or Linux workstations using Python 3.10+.

### Prerequisites
- Python 3.12 installed and on `PATH`
- Git repository checked out at `c:\work\isolate`

### Setup Instructions (PowerShell on Windows)
```powershell
# 1. Navigate to isolate root
cd c:\work\isolate

# 2. Create project virtual environment (git-ignored)
python -m venv .venv

# 3. Activate the environment
.\.venv\Scripts\Activate.ps1

# 4. Install declared dependencies
pip install -r network\mcp\requirements.txt
```

### Launching the Gateway
```powershell
# Direct launcher (binds localhost:8720)
.\network\mcp\etc\start-comfy-gateway.cmd
```

Alternatively, run as a module directly:
```powershell
$env:PYTHONPATH = "$PWD\network\mcp"
python -m comfy_gateway.kernel.entrypoint
```

---

## 3. Option B: Containerized Setup (Docker / Podman)

Containerization isolates Python dependencies, standardizes Linux toolchains, and enables running headless on remote servers (e.g. AM4).

### Container Specification (`c:\work\isolate\network\mcp\Dockerfile`)
The gateway container builds from `python:3.12-slim`:

```dockerfile
FROM python:3.12-slim

WORKDIR /app

# Install system dependencies (SSH client for fleet management)
RUN apt-get update && apt-get install -y --no-install-recommends \
    openssh-client \
    curl \
    git \
    && rm -rf /var/lib/apt/lists/*

# Copy and install requirements
COPY network/mcp/requirements.txt /app/
RUN pip install --no-cache-dir -r requirements.txt

# Copy gateway source code
COPY network/mcp/ /app/network/mcp/

ENV PYTHONPATH="/app/network/mcp"
ENV COMFY_MCP_PORT=8720

EXPOSE 8720

CMD ["python", "-m", "comfy_gateway.kernel.entrypoint"]
```

### Docker Compose Profile
Run via Docker Compose with volume mounts for SSH keys and fleet cache:

```yaml
version: "3.8"

services:
  comfy-gateway:
    build:
      context: .
      dockerfile: network/mcp/Dockerfile
    container_name: comfy-mcp-gateway
    restart: unless-stopped
    ports:
      - "127.0.0.1:8722:8720"
    environment:
      - COMFY_MCP_PORT=8720
      - X_COMFY_KEY=comfy-dev-local
    volumes:
      # Mount SSH keys for remote fleet discovery (AM4, FX99, i5)
      - ${HOME}/.ssh:/root/.ssh:ro
      # Persist fleet telemetry and cache
      - ./network/mcp/var:/app/network/mcp/var
```

### Running with Docker
```bash
# Build and run container
docker compose up -d comfy-gateway

# Check health
curl http://127.0.0.1:8722/healthz
```

---

## 4. MCP Client Configuration

### Connecting Antigravity / Claude Desktop
Add the following to your MCP client configuration (`mcp_config.json` or `claude_desktop_config.json`):

#### Local stdio Connection:
```json
{
  "mcpServers": {
    "valheim-fleet": {
      "command": "c:\\work\\isolate\\.venv\\Scripts\\python.exe",
      "args": [
        "-m",
        "comfy_gateway.kernel.entrypoint"
      ],
      "env": {
        "PYTHONPATH": "c:\\work\\isolate\\network\\mcp"
      }
    }
  }
}
```

#### HTTP / SSE Connection:
```json
{
  "mcpServers": {
    "valheim-fleet": {
      "url": "http://127.0.0.1:8722/mcp",
      "headers": {
        "X-Comfy-Key": "comfy-dev-local"
      }
    }
  }
}
```
