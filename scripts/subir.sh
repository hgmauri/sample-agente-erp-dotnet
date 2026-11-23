#!/usr/bin/env bash
# Sobe os três serviços em segundo plano. Ctrl+C encerra todos.
set -e
cd "$(dirname "$0")/.."
dotnet build sample-agente-erp-dotnet.slnx
trap 'kill 0' EXIT
dotnet run --project src/Erp.Simulado --no-build &
dotnet run --project src/Dados.Api --no-build &
dotnet run --project src/Assistente.Api --no-build &
wait
