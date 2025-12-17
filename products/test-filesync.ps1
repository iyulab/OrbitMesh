# OrbitMesh File Sync Test Script
# This script helps you test file synchronization between two nodes
#
# Prerequisites:
# 1. Server is running (start-server.cmd)
# 2. Node1 is running (start-node1.cmd)
# 3. Node2 is running (start-node2.cmd)

param(
    [Parameter(Position=0)]
    [ValidateSet("status", "start-watch", "stop-watch", "list-watches", "create-file", "sync")]
    [string]$Action = "status",

    [string]$Node = "node1",
    [string]$Path = "D:\node1",
    [string]$FileName = "test.txt",
    [string]$Content = "Hello from file sync test!"
)

$ServerUrl = "http://localhost:5000"

function Get-Status {
    Write-Host "Checking server status..." -ForegroundColor Cyan
    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/agents" -Method Get
        Write-Host "Connected Agents:" -ForegroundColor Green
        $response | ForEach-Object {
            Write-Host "  - Name: $($_.name), ID: $($_.id), Status: $($_.status)" -ForegroundColor White
        }
    }
    catch {
        Write-Host "Error: Server not reachable. Make sure the server is running." -ForegroundColor Red
        Write-Host $_.Exception.Message -ForegroundColor Yellow
    }
}

function Start-FileWatch {
    param($NodeName, $WatchPath)

    Write-Host "Starting file watch on $NodeName for path: $WatchPath" -ForegroundColor Cyan

    $body = @{
        command = "orbit:filewatch:start"
        payload = @{
            watchId = "sync-$NodeName"
            path = $WatchPath
            filter = "*"
            includeSubdirectories = $true
            debounceMs = 500
        }
        agentPattern = $NodeName
    } | ConvertTo-Json -Depth 3

    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/jobs" -Method Post -Body $body -ContentType "application/json"
        Write-Host "Job created: $($response.jobId)" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Stop-FileWatch {
    param($NodeName)

    Write-Host "Stopping file watch on $NodeName" -ForegroundColor Cyan

    $body = @{
        command = "orbit:filewatch:stop"
        payload = @{
            watchId = "sync-$NodeName"
        }
        agentPattern = $NodeName
    } | ConvertTo-Json -Depth 3

    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/jobs" -Method Post -Body $body -ContentType "application/json"
        Write-Host "Job created: $($response.jobId)" -ForegroundColor Green
        return $response
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Get-FileWatches {
    param($NodeName)

    Write-Host "Listing file watches on $NodeName" -ForegroundColor Cyan

    $body = @{
        command = "orbit:filewatch:list"
        payload = @{}
        agentPattern = $NodeName
    } | ConvertTo-Json -Depth 3

    try {
        $response = Invoke-RestMethod -Uri "$ServerUrl/api/jobs" -Method Post -Body $body -ContentType "application/json"
        Write-Host "Job created: $($response.jobId)" -ForegroundColor Green
        Write-Host "Check job result at: $ServerUrl/api/jobs/$($response.jobId)" -ForegroundColor Yellow
        return $response
    }
    catch {
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Create-TestFile {
    param($FilePath, $FileContent)

    Write-Host "Creating test file: $FilePath" -ForegroundColor Cyan
    $FileContent | Out-File -FilePath $FilePath -Encoding UTF8
    Write-Host "File created successfully" -ForegroundColor Green
}

switch ($Action) {
    "status" {
        Get-Status
    }
    "start-watch" {
        Start-FileWatch -NodeName $Node -WatchPath $Path
    }
    "stop-watch" {
        Stop-FileWatch -NodeName $Node
    }
    "list-watches" {
        Get-FileWatches -NodeName $Node
    }
    "create-file" {
        $fullPath = Join-Path $Path $FileName
        Create-TestFile -FilePath $fullPath -FileContent $Content
    }
    "sync" {
        Write-Host "Setting up file watches on both nodes..." -ForegroundColor Cyan
        Start-FileWatch -NodeName "node1" -WatchPath "D:\node1"
        Start-Sleep -Seconds 1
        Start-FileWatch -NodeName "node2" -WatchPath "D:\node2"
        Write-Host ""
        Write-Host "File watches started on both nodes!" -ForegroundColor Green
        Write-Host "Now you can:" -ForegroundColor Yellow
        Write-Host "  1. Create a file in D:\node1 - it should sync to D:\node2" -ForegroundColor White
        Write-Host "  2. Create a file in D:\node2 - it should sync to D:\node1" -ForegroundColor White
    }
}

Write-Host ""
Write-Host "Usage Examples:" -ForegroundColor Gray
Write-Host "  .\test-filesync.ps1 status                    # Check server and agent status" -ForegroundColor DarkGray
Write-Host "  .\test-filesync.ps1 sync                      # Start file watches on both nodes" -ForegroundColor DarkGray
Write-Host "  .\test-filesync.ps1 start-watch -Node node1   # Start watch on specific node" -ForegroundColor DarkGray
Write-Host "  .\test-filesync.ps1 create-file -Path D:\node1 -FileName test.txt" -ForegroundColor DarkGray
