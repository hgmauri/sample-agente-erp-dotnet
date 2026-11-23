using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Dados.Api.Auditoria;

public sealed record RegistroDeAcesso(DateTimeOffset Quando, string Usuario, string Origem, string Requisicao, int Status);

/// <summary>Trilha de auditoria em memória para o exemplo. Em produção: Application Insights, Log Analytics ou tabela própria.</summary>
public sealed class RegistroDeAuditoria
{
    private const int Capacidade = 1000;
    private readonly ConcurrentQueue<RegistroDeAcesso> _registros = new();

    public void Registrar(RegistroDeAcesso registro)
    {
        _registros.Enqueue(registro);
        while (_registros.Count > Capacidade && _registros.TryDequeue(out _)) { }
    }

    public IReadOnlyList<RegistroDeAcesso> Ultimos(int quantidade) =>
        [.. _registros.Reverse().Take(quantidade)];
}

/// <summary>Registra toda consulta a /v1: quem, de onde (app ou assistente), o quê e o resultado.</summary>
public sealed partial class MiddlewareDeAuditoria(RequestDelegate proximo, RegistroDeAuditoria auditoria, ILogger<MiddlewareDeAuditoria> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        await proximo(ctx);

        if (!ctx.Request.Path.StartsWithSegments("/v1"))
            return;

        var origem = ctx.Request.Headers["X-Origem"].ToString();
        var registro = new RegistroDeAcesso(
            DateTimeOffset.UtcNow,
            ctx.User.Identity?.Name ?? "anonimo",
            OrigemValida().IsMatch(origem) ? origem : "app",
            ctx.Request.Path + ctx.Request.QueryString,
            ctx.Response.StatusCode);

        auditoria.Registrar(registro);
        LogAcesso(logger, registro.Usuario, registro.Origem, registro.Requisicao, registro.Status);
    }

    [GeneratedRegex("^[a-z]{1,20}$")]
    private static partial Regex OrigemValida();

    [LoggerMessage(Level = LogLevel.Information, Message = "Acesso {Usuario} via {Origem}: {Requisicao} -> {Status}")]
    private static partial void LogAcesso(ILogger logger, string usuario, string origem, string requisicao, int status);
}
