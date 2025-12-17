# OrbitMesh File Sync Test - Startup Script
# Usage: .\start-filesync-test.ps1

param(
    [switch]$BuildFirst,
    [switch]$ServerOnly,
    [switch]$Node1Only,
    [switch]$Node2Only
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir = Split-Path -Parent $ScriptDir

Write-Host "======================================" -ForegroundColor Cyan
Write-Host "OrbitMesh File Sync Test Environment" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Create test directories if they don't exist
$dirs = @("D:\node1", "D:\node2", "D:\sync-hub")
foreach ($dir in $dirs) {
    if (-not (Test-Path $dir)) {
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Green
    }
}

# Build if requested
if ($BuildFirst) {
    Write-Host "Building solution..." -ForegroundColor Yellow
    Push-Location $RootDir
    dotnet build OrbitMesh.sln -c Debug
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed!" -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location
    Write-Host "Build completed successfully" -ForegroundColor Green
}

Write-Host ""
Write-Host "Test Configuration:" -ForegroundColor Yellow
Write-Host "  Server URL:     http://localhost:5000" -ForegroundColor White
Write-Host "  Node1 Path:     D:\node1" -ForegroundColor White
Write-Host "  Node2 Path:     D:\node2" -ForegroundColor White
Write-Host "  Sync Hub:       D:\sync-hub" -ForegroundColor White
Write-Host ""

# Function to start server
function Start-Server {
    Write-Host "Starting OrbitMesh Server..." -ForegroundColor Cyan
    $serverPath = Join-Path $ScriptDir "orbit-host"
    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$serverPath\orbit-host.csproj", "--", "--urls=http://localhost:5000" -WorkingDirectory $serverPath -PassThru
}

# Function to start node agent
function Start-Node {
    param($NodeName, $ConfigFile)
    Write-Host "Starting $NodeName agent..." -ForegroundColor Cyan
    $nodePath = Join-Path $ScriptDir "orbit-node"
    $env:ASPNETCORE_ENVIRONMENT = $NodeName
    Start-Process -FilePath "dotnet" -ArgumentList "run", "--project", "$nodePath\orbit-node.csproj" -WorkingDirectory $nodePath -PassThru
}

if ($ServerOnly) {
    Start-Server
    Write-Host ""
    Write-Host "Server started. Press any key to exit..." -ForegroundColor Yellow
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
}
elseif ($Node1Only) {
    Write-Host "Starting Node1 agent only..." -ForegroundColor Yellow
    $nodePath = Join-Path $ScriptDir "orbit-node"
    Push-Location $nodePath
    $env:OrbitMesh__AgentName = "node1"
    dotnet run
    Pop-Location
}
elseif ($Node2Only) {
    Write-Host "Starting Node2 agent only..." -ForegroundColor Yellow
    $nodePath = Join-Path $ScriptDir "orbit-node"
    Push-Location $nodePath
    $env:OrbitMesh__AgentName = "node2"
    dotnet run
    Pop-Location
}
else {
    Write-Host "To test file sync, open 3 separate terminals and run:" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Terminal 1 (Server):" -ForegroundColor Cyan
    Write-Host "  cd $ScriptDir\orbit-host" -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    Write-Host ""
    Write-Host "Terminal 2 (Node1):" -ForegroundColor Cyan
    Write-Host "  cd $ScriptDir\orbit-node" -ForegroundColor White
    Write-Host '  $env:OrbitMesh__AgentName = "node1"' -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    Write-Host ""
    Write-Host "Terminal 3 (Node2):" -ForegroundColor Cyan
    Write-Host "  cd $ScriptDir\orbit-node" -ForegroundColor White
    Write-Host '  $env:OrbitMesh__AgentName = "node2"' -ForegroundColor White
    Write-Host "  dotnet run" -ForegroundColor White
    Write-Host ""
    Write-Host "After all components are running:" -ForegroundColor Yellow
    Write-Host "1. Open http://localhost:5000/swagger to access API" -ForegroundColor White
    Write-Host "2. Start file watches using the workflow API" -ForegroundColor White
    Write-Host "3. Create/modify files in D:\node1 or D:\node2" -ForegroundColor White
    Write-Host "4. Watch the synchronization in action" -ForegroundColor White
}
