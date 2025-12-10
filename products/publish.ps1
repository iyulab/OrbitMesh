<#
.SYNOPSIS
    Publish OrbitMesh Server and Agent as single-file executables.

.DESCRIPTION
    This script builds and publishes OrbitMeshServer.exe and OrbitMeshAgent.exe
    as self-contained single-file executables for Windows x64.

.PARAMETER OutputDir
    Output directory for published files. Default: ./publish

.PARAMETER Runtime
    Target runtime identifier. Default: win-x64
    Options: win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

.PARAMETER Configuration
    Build configuration. Default: Release

.EXAMPLE
    .\publish.ps1
    .\publish.ps1 -OutputDir "C:\Deploy" -Runtime "linux-x64"
#>

param(
    [string]$OutputDir = "./publish",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Step($message) { Write-Host "`n>> $message" -ForegroundColor Cyan }
function Write-Success($message) { Write-Host "   $message" -ForegroundColor Green }
function Write-Info($message) { Write-Host "   $message" -ForegroundColor Gray }

# Paths
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$SolutionRoot = Split-Path -Parent $ScriptDir
$ServerProject = Join-Path $ScriptDir "orbit-server\orbit-server.csproj"
$AgentProject = Join-Path $ScriptDir "orbit-agent\orbit-agent.csproj"

$ServerOutput = Join-Path $OutputDir "server"
$AgentOutput = Join-Path $OutputDir "agent"

Write-Host "============================================" -ForegroundColor Yellow
Write-Host "  OrbitMesh Publisher" -ForegroundColor Yellow
Write-Host "============================================" -ForegroundColor Yellow
Write-Info "Runtime: $Runtime"
Write-Info "Configuration: $Configuration"
Write-Info "Output: $OutputDir"

# Clean output directories
Write-Step "Cleaning output directories..."
if (Test-Path $ServerOutput) { Remove-Item -Recurse -Force $ServerOutput }
if (Test-Path $AgentOutput) { Remove-Item -Recurse -Force $AgentOutput }
New-Item -ItemType Directory -Force -Path $ServerOutput | Out-Null
New-Item -ItemType Directory -Force -Path $AgentOutput | Out-Null
Write-Success "Done"

# Publish Server
Write-Step "Publishing OrbitMesh Server..."
dotnet publish $ServerProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --output $ServerOutput `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish Server" -ForegroundColor Red
    exit 1
}
Write-Success "Server published successfully"

# Publish Agent
Write-Step "Publishing OrbitMesh Agent..."
dotnet publish $AgentProject `
    --configuration $Configuration `
    --runtime $Runtime `
    --output $AgentOutput `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to publish Agent" -ForegroundColor Red
    exit 1
}
Write-Success "Agent published successfully"

# List output files
Write-Step "Published files:"

Write-Host "`n  Server ($ServerOutput):" -ForegroundColor White
Get-ChildItem $ServerOutput | ForEach-Object {
    $size = "{0:N2} MB" -f ($_.Length / 1MB)
    Write-Info "    $($_.Name) ($size)"
}

Write-Host "`n  Agent ($AgentOutput):" -ForegroundColor White
Get-ChildItem $AgentOutput | ForEach-Object {
    $size = "{0:N2} MB" -f ($_.Length / 1MB)
    Write-Info "    $($_.Name) ($size)"
}

Write-Host "`n============================================" -ForegroundColor Green
Write-Host "  Publish completed successfully!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "`nTo run:" -ForegroundColor White
Write-Host "  Server: $ServerOutput\OrbitMeshServer.exe" -ForegroundColor Gray
Write-Host "  Agent:  $AgentOutput\OrbitMeshAgent.exe" -ForegroundColor Gray
