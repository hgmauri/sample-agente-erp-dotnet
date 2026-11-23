# Sobe os três serviços, cada um em uma janela.
Set-Location (Join-Path $PSScriptRoot "..")
dotnet build sample-agente-erp-dotnet.slnx
foreach ($projeto in "Erp.Simulado", "Dados.Api", "Assistente.Api") {
    Start-Process dotnet -ArgumentList "run --project src/$projeto --no-build"
}
