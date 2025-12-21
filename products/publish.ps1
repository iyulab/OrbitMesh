<#
.SYNOPSIS
    Publish OrbitMesh Server and Agent with maximum performance optimization.
.PARAMETER Mode
    standard, optimized (default), aot
.PARAMETER Runtime
    win-x64 (default), linux-x64, osx-x64, etc.
.PARAMETER Target
    all (default), server, agent
.EXAMPLE
    .\publish.ps1 -Mode aot -Runtime linux-x64
#>

param(
    [string]$OutputDir = "./publish",
    [ValidateSet("win-x64", "win-arm64", "linux-x64", "linux-arm64", "linux-musl-x64", "linux-musl-arm64", "osx-x64", "osx-arm64")]
    [string]$Runtime = "win-x64",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateSet("standard", "optimized", "aot")]
    [string]$Mode = "optimized",
    [ValidateSet("all", "server", "agent")]
    [string]$Target = "all",
    [switch]$NoBanner
)

$ErrorActionPreference = "Stop"
$StartTime = Get-Date

function Write-Step($msg) { Write-Host "`n>> $msg" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "   [OK] $msg" -ForegroundColor Green }
function Write-Warning($msg) { Write-Host "   [WARN] $msg" -ForegroundColor Yellow }
function Write-Info($msg) { Write-Host "   $msg" -ForegroundColor Gray }
function Write-Err($msg) { Write-Host "   [ERROR] $msg" -ForegroundColor Red }

function Get-FileSize($p) {
    if (Test-Path $p) {
        $s = (Get-Item $p).Length
        if ($s -ge 1GB) { return "{0:N2} GB" -f ($s / 1GB) }
        if ($s -ge 1MB) { return "{0:N2} MB" -f ($s / 1MB) }
        if ($s -ge 1KB) { return "{0:N2} KB" -f ($s / 1KB) }
        return "$s bytes"
    }
    return "N/A"
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ServerProject = Join-Path $ScriptDir "orbit-host\orbit-host.csproj"
$AgentProject = Join-Path $ScriptDir "orbit-node\orbit-node.csproj"
$ExeExt = if ($Runtime -like "win-*") { ".exe" } else { "" }
$ServerOutput = Join-Path $OutputDir "server"
$AgentOutput = Join-Path $OutputDir "agent"

if (-not $NoBanner) {
    Write-Host "`n  =======================================================`n         OrbitMesh High-Performance Publisher`n  =======================================================`n" -ForegroundColor Magenta
}

$ModeDesc = switch ($Mode) {
    "standard"  { "Single-file + Compression" }
    "optimized" { "Single-file + R2R (Fast Startup)" }
    "aot"       { "Native AOT (Max Performance)" }
}

Write-Host "  Config: Mode=$($Mode.ToUpper()) [$ModeDesc]" -ForegroundColor White
Write-Host "          Runtime=$Runtime Target=$Target Output=$OutputDir" -ForegroundColor White

$CommonArgs = @("--configuration", $Configuration, "--runtime", $Runtime, "--self-contained", "true", "-p:DebugType=none", "-p:DebugSymbols=false")

switch ($Mode) {
    "standard" { $CommonArgs += "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true" }
    "optimized" { $CommonArgs += "-p:PublishSingleFile=true", "-p:EnableCompressionInSingleFile=true", "-p:IncludeNativeLibrariesForSelfExtract=true", "-p:PublishReadyToRun=true", "-p:PublishReadyToRunComposite=true", "-p:TieredCompilation=false", "-p:TieredPGO=false" }
    "aot" { $CommonArgs += "-p:PublishAot=true", "-p:StripSymbols=true", "-p:OptimizationPreference=Speed", "-p:IlcGenerateStackTraceData=false", "-p:IlcOptimizationPreference=Speed", "-p:IlcFoldIdenticalMethodBodies=true", "-p:InvariantGlobalization=true" }
}

Write-Step "Pre-build checks..."
$dv = dotnet --version 2>$null
if (-not $dv) { Write-Err ".NET SDK not found."; exit 1 }
Write-Success ".NET SDK: $dv"

$nv = node --version 2>$null
if (-not $nv) { Write-Err "Node.js not found."; exit 1 }
Write-Success "Node.js: $nv"

if (($Target -eq "all" -or $Target -eq "server") -and -not (Test-Path $ServerProject)) { Write-Err "Server not found: $ServerProject"; exit 1 }
if (($Target -eq "all" -or $Target -eq "agent") -and -not (Test-Path $AgentProject)) { Write-Err "Agent not found: $AgentProject"; exit 1 }
Write-Success "Projects found"

if ($Mode -eq "aot") {
    Write-Warning "AOT mode has limitations:"
    Write-Info "  - ASP.NET Core MVC does NOT support Native AOT"
    Write-Info "  - No runtime code generation (reflection limitations)"
    Write-Info "  - EF Core, MessagePack, Swagger may have issues"
    Write-Info "  - Use only for simple console apps or APIs without MVC"
}

Write-Step "Preparing directories..."
if ($Target -eq "all" -or $Target -eq "server") { if (Test-Path $ServerOutput) { Remove-Item -Recurse -Force $ServerOutput }; New-Item -ItemType Directory -Force -Path $ServerOutput | Out-Null }
if ($Target -eq "all" -or $Target -eq "agent") { if (Test-Path $AgentOutput) { Remove-Item -Recurse -Force $AgentOutput }; New-Item -ItemType Directory -Force -Path $AgentOutput | Out-Null }
Write-Success "Ready"

if ($Target -eq "all" -or $Target -eq "server") {
    # Build frontend first
    $ClientAppDir = Join-Path $ScriptDir "orbit-host\ClientApp"
    if (Test-Path $ClientAppDir) {
        Write-Step "Building Frontend (ClientApp)..."
        Push-Location $ClientAppDir
        try {
            $ft = Get-Date
            Write-Info "npm run build"
            & npm run build
            if ($LASTEXITCODE -ne 0) { Write-Err "Frontend build failed"; exit 1 }
            $fd = (Get-Date) - $ft
            Write-Success "Frontend done - Time: $([math]::Round($fd.TotalSeconds, 1))s"
        }
        finally {
            Pop-Location
        }
    }

    Write-Step "Publishing Server..."
    $sArgs = @("publish", $ServerProject, "--output", $ServerOutput) + $CommonArgs
    Write-Info "dotnet $($sArgs -join ' ')"
    Write-Host ""
    $st = Get-Date
    & dotnet @sArgs
    if ($LASTEXITCODE -ne 0) { Write-Err "Server publish failed"; exit 1 }
    $sd = (Get-Date) - $st
    $serverExe = Join-Path $ServerOutput "OrbitMeshServer$ExeExt"
    Write-Success "Server done - Size: $(Get-FileSize $serverExe) Time: $([math]::Round($sd.TotalSeconds, 1))s"
}

if ($Target -eq "all" -or $Target -eq "agent") {
    Write-Step "Publishing Agent..."
    $aArgs = @("publish", $AgentProject, "--output", $AgentOutput) + $CommonArgs
    Write-Info "dotnet $($aArgs -join ' ')"
    Write-Host ""
    $at = Get-Date
    & dotnet @aArgs
    if ($LASTEXITCODE -ne 0) { Write-Err "Agent publish failed"; exit 1 }
    $ad = (Get-Date) - $at
    $agentExe = Join-Path $AgentOutput "OrbitMeshAgent$ExeExt"
    Write-Success "Agent done - Size: $(Get-FileSize $agentExe) Time: $([math]::Round($ad.TotalSeconds, 1))s"
}

$td = (Get-Date) - $StartTime
Write-Host "`n  =======================================================" -ForegroundColor Green
Write-Host "           Publish Completed! Total: $([math]::Round($td.TotalSeconds, 1))s" -ForegroundColor Green
Write-Host "  =======================================================`n" -ForegroundColor Green

Write-Host "  Published Files:" -ForegroundColor White
if ($Target -eq "all" -or $Target -eq "server") {
    Write-Host "    Server ($ServerOutput):" -ForegroundColor Cyan
    Get-ChildItem $ServerOutput | ForEach-Object { Write-Host "      $($_.Name) ($(Get-FileSize $_.FullName))" -ForegroundColor Gray }
}
if ($Target -eq "all" -or $Target -eq "agent") {
    Write-Host "    Agent ($AgentOutput):" -ForegroundColor Cyan
    Get-ChildItem $AgentOutput | ForEach-Object { Write-Host "      $($_.Name) ($(Get-FileSize $_.FullName))" -ForegroundColor Gray }
}

Write-Host "`n  Optimizations: $ModeDesc" -ForegroundColor DarkGray
Write-Host "`n  To run:" -ForegroundColor White
if ($Target -eq "all" -or $Target -eq "server") { Write-Host "    Server: $ServerOutput\OrbitMeshServer$ExeExt" -ForegroundColor Cyan }
if ($Target -eq "all" -or $Target -eq "agent") { Write-Host "    Agent:  $AgentOutput\OrbitMeshAgent$ExeExt" -ForegroundColor Cyan }
Write-Host ""
