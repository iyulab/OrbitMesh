@echo off
title OrbitMesh Node1
cd /d %~dp0orbit-node
set AGENT_NAME=node1
echo Starting OrbitMesh Node1 Agent...
echo Watch Path: D:\node1
dotnet run --no-build
pause
