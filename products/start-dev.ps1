# OrbitMesh Development Environment Launcher
# Launches orbit-host, orbit-node, and React dev server in Windows Terminal tabs

param(
    [int]$AgentCount = 1,
    [switch]$NoBuild,
    [switch]$NoUI,
    [string]$ServerUrl = "http://localhost:5000/agent"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir

Write-Host "OrbitMesh Development Environment" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Check if Windows Terminal is available
$wtPath = Get-Command "wt.exe" -ErrorAction SilentlyContinue
if (-not $wtPath) {
    Write-Host "Error: Windows Terminal (wt.exe) not found." -ForegroundColor Red
    Write-Host "Please install Windows Terminal from Microsoft Store or winget:" -ForegroundColor Yellow
    Write-Host "  winget install Microsoft.WindowsTerminal" -ForegroundColor Gray
    exit 1
}

# Project paths
$serverProject = Join-Path $ScriptDir "orbit-host\orbit-host.csproj"
$agentProject = Join-Path $ScriptDir "orbit-node\orbit-node.csproj"
$clientAppDir = Join-Path $ScriptDir "orbit-host\ClientApp"

# Check if ClientApp has node_modules
$nodeModulesDir = Join-Path $clientAppDir "node_modules"
if (-not $NoUI -and -not (Test-Path $nodeModulesDir)) {
    Write-Host "Installing React app dependencies..." -ForegroundColor Yellow
    Push-Location $clientAppDir
    try {
        npm install
        if ($LASTEXITCODE -ne 0) {
            Write-Host "npm install failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Dependencies installed!" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# Build projects if not skipped
if (-not $NoBuild) {
    Write-Host "Building OrbitMesh solution..." -ForegroundColor Yellow
    Push-Location $RootDir
    try {
        dotnet build --configuration Debug --verbosity quiet
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed!" -ForegroundColor Red
            exit 1
        }
        Write-Host "Build successful!" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "Starting services:" -ForegroundColor Yellow
Write-Host "  - orbit-host (port 5000)" -ForegroundColor Gray
if (-not $NoUI) {
    Write-Host "  - React dev server (port 5173)" -ForegroundColor Gray
}
Write-Host "  - orbit-node x$AgentCount" -ForegroundColor Gray
Write-Host ""

# Create temporary script files for each tab
$tempDir = Join-Path $env:TEMP "OrbitMesh"
if (-not (Test-Path $tempDir)) {
    New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
}

# Server script
$serverScript = Join-Path $tempDir "run-server.ps1"
@"
`$Host.UI.RawUI.WindowTitle = 'orbit-host'
`$env:ASPNETCORE_ENVIRONMENT = 'Development'
Write-Host 'Starting OrbitMesh Server (Development)...' -ForegroundColor Cyan
dotnet run --project "$serverProject" --no-build
"@ | Set-Content -Path $serverScript -Encoding UTF8

# React dev server script
$uiScript = Join-Path $tempDir "run-ui.ps1"
@"
`$Host.UI.RawUI.WindowTitle = 'React UI'
Set-Location "$clientAppDir"
Write-Host 'Starting React Development Server...' -ForegroundColor Cyan
Write-Host 'UI will be available at http://localhost:5173' -ForegroundColor Yellow
Write-Host ''
npm run dev
"@ | Set-Content -Path $uiScript -Encoding UTF8

# Agent scripts
$agentScripts = @()
for ($i = 1; $i -le $AgentCount; $i++) {
    $agentName = "dev-agent-$i"
    $delay = 3 + $i
    $agentScript = Join-Path $tempDir "run-agent-$i.ps1"
    @"
`$Host.UI.RawUI.WindowTitle = '$agentName'
Write-Host 'Waiting $delay seconds for server startup...' -ForegroundColor Yellow
Start-Sleep -Seconds $delay
Write-Host 'Starting OrbitMesh Agent: $agentName' -ForegroundColor Cyan
dotnet run --project "$agentProject" --no-build -- --OrbitMesh:ServerUrl="$ServerUrl" --OrbitMesh:AgentName="$agentName"
"@ | Set-Content -Path $agentScript -Encoding UTF8
    $agentScripts += $agentScript
}

# Build Windows Terminal arguments
$wtArgs = @()

# Server tab
$wtArgs += "new-tab"
$wtArgs += "--title"
$wtArgs += "`"orbit-host`""
$wtArgs += "--tabColor"
$wtArgs += "`"#0078D4`""
$wtArgs += "pwsh"
$wtArgs += "-NoExit"
$wtArgs += "-File"
$wtArgs += "`"$serverScript`""

# React UI tab
if (-not $NoUI) {
    $wtArgs += ";"
    $wtArgs += "new-tab"
    $wtArgs += "--title"
    $wtArgs += "`"React-UI`""
    $wtArgs += "--tabColor"
    $wtArgs += "`"#61DAFB`""
    $wtArgs += "pwsh"
    $wtArgs += "-NoExit"
    $wtArgs += "-File"
    $wtArgs += "`"$uiScript`""
}

# Agent tabs
for ($i = 0; $i -lt $agentScripts.Count; $i++) {
    $agentName = "dev-agent-$($i + 1)"
    $tabColor = if (($i + 1) % 2 -eq 0) { "#107C10" } else { "#00B294" }
    $wtArgs += ";"
    $wtArgs += "new-tab"
    $wtArgs += "--title"
    $wtArgs += "`"$agentName`""
    $wtArgs += "--tabColor"
    $wtArgs += "`"$tabColor`""
    $wtArgs += "pwsh"
    $wtArgs += "-NoExit"
    $wtArgs += "-File"
    $wtArgs += "`"$($agentScripts[$i])`""
}

Write-Host "Launching Windows Terminal..." -ForegroundColor Cyan
Start-Process "wt.exe" -ArgumentList $wtArgs

Write-Host ""
Write-Host "Development environment started!" -ForegroundColor Green
Write-Host ""
Write-Host "Endpoints:" -ForegroundColor Yellow
Write-Host "  Web UI:        http://localhost:5173" -ForegroundColor Gray
Write-Host "  Server API:    http://localhost:5000" -ForegroundColor Gray
Write-Host "  Swagger UI:    http://localhost:5000/swagger" -ForegroundColor Gray
Write-Host "  Health Check:  http://localhost:5000/health" -ForegroundColor Gray
Write-Host "  Agent Hub:     http://localhost:5000/agent" -ForegroundColor Gray
Write-Host ""
Write-Host "The React dev server proxies /api and /agent to the backend." -ForegroundColor DarkGray
Write-Host "Press Ctrl+C in each tab to stop the services." -ForegroundColor DarkGray
