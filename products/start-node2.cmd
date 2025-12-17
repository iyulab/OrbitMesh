@echo off
title OrbitMesh Node2
cd /d %~dp0orbit-node
set AGENT_NAME=node2
echo Starting OrbitMesh Node2 Agent...
echo Watch Path: D:\node2
dotnet run --no-build
pause
