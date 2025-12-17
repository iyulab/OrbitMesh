@echo off
title OrbitMesh Server
cd /d %~dp0orbit-host
echo Starting OrbitMesh Server...
dotnet run --no-build
pause
