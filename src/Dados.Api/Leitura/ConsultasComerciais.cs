using Dados.Api.Acesso;

namespace Dados.Api.Leitura;

/// <summary>
/// Endpoints de leitura com propósito definido. Não existe "consulta genérica",
/// nem SQL livre, nem endpoint de escrita: o que o agente pode fazer é exatamente o que está aqui.
/// </summary>
public static class ConsultasComerciais
{
    private const int TamanhoMaximoDePagina = 50;
    private const int PeriodoMaximoEmDias = 90;

    public static RouteGroupBuilder MapConsultasComerciais(this RouteGroupBuilder v1)
    {
        v1.MapGet("/pedidos", async (string? situacao, int? pagina, int? tamanho,
            HttpContext ctx, LeituraComercial leitura, CancellationToken ct) =>
        {
            situacao = (situacao ?? "todos").ToLowerInvariant();
            if (situacao is not ("todos" or "abertos" or "atrasados"))
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["situacao"] = ["Use todos, abertos ou atrasados."],
                });

            var numeroDaPagina = Math.Max(1, pagina ?? 1);
            var tamanhoDaPagina = Math.Clamp(tamanho ?? 20, 1, TamanhoMaximoDePagina);

            var pedidos = (await leitura.PedidosAsync(ctx.Escopo(), ct))
                .Where(p => situacao switch
                {
                    "abertos" => p.Situacao is "aberto" or "faturado",
                    "atrasados" => p.Atrasado,
                    _ => true,
                })
                .OrderByDescending(p => p.Emissao)
                .ToList();

            var itens = pedidos.Skip((numeroDaPagina - 1) * tamanhoDaPagina).Take(tamanhoDaPagina).ToList();
            return Results.Ok(new Pagina<PedidoDto>(itens, numeroDaPagina, tamanhoDaPagina, pedidos.Count));
        });

        v1.MapGet("/pedidos/{numero:int}", async (int numero, HttpContext ctx, LeituraComercial leitura, CancellationToken ct) =>
            await leitura.PedidoAsync(ctx.Escopo(), numero, ct) is { } pedido
                ? Results.Ok(pedido)
                : Results.NotFound());

        v1.MapGet("/estoque/{produto:int}", async (int produto, HttpContext ctx, LeituraComercial leitura, CancellationToken ct) =>
            await leitura.EstoqueAsync(ctx.Escopo(), produto, ct) is { } estoque
                ? Results.Ok(estoque)
                : Results.NotFound());

        v1.MapGet("/clientes/{codigo:int}/situacao", async (int codigo, HttpContext ctx, LeituraComercial leitura, CancellationToken ct) =>
            await leitura.SituacaoDoClienteAsync(ctx.Escopo(), codigo, ct) is { } situacao
                ? Results.Ok(situacao)
                : Results.NotFound());

        v1.MapGet("/vendas/resumo", async (int? dias, HttpContext ctx, LeituraComercial leitura, CancellationToken ct) =>
            Results.Ok(await leitura.ResumoDeVendasAsync(ctx.Escopo(), Math.Clamp(dias ?? 30, 1, PeriodoMaximoEmDias), ct)));

        return v1;
    }
}
