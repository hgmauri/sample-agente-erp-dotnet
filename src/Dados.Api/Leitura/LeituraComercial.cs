using Dados.Api.Acesso;
using Dados.Api.Erp;

namespace Dados.Api.Leitura;

/// <summary>
/// Traduz o ERP para o negócio: aplica o escopo do usuário, converte formatos,
/// calcula situação, atraso, saldo e totais. Conta é feita aqui, em código, e não pelo modelo.
/// </summary>
public sealed class LeituraComercial(ClienteErp erp, TimeProvider relogio)
{
    private DateOnly Hoje => DateOnly.FromDateTime(relogio.GetLocalNow().Date);

    public async Task<IReadOnlyList<PedidoDto>> PedidosAsync(Escopo escopo, CancellationToken ct)
    {
        // O filtro pelo vendedor vai na consulta ao ERP, e o escopo é conferido de novo em cada linha
        var linhas = escopo.CodigoVendedor is int vendedor
            ? await erp.ConsultarAsync("TGFCAB", "CODVEND", vendedor, ct)
            : await erp.ConsultarAsync("TGFCAB", ct: ct);

        return await MontarPedidosAsync(escopo, [.. linhas.Where(escopo.PodeVer)], ct);
    }

    public async Task<PedidoDto?> PedidoAsync(Escopo escopo, int numero, CancellationToken ct)
    {
        var linhas = await erp.ConsultarAsync("TGFCAB", "NUNOTA", numero, ct);
        var visiveis = linhas.Where(escopo.PodeVer).ToList();

        // Pedido de outro vendedor e pedido inexistente têm a mesma resposta: não encontrado
        return visiveis.Count == 0 ? null : (await MontarPedidosAsync(escopo, visiveis, ct))[0];
    }

    public async Task<EstoqueDto?> EstoqueAsync(Escopo escopo, int produto, CancellationToken ct)
    {
        if (await erp.ConsultarAsync("TGFPRO", "CODPROD", produto, ct) is not [var cadastro, ..])
            return null;

        var filiais = await NomesDasFiliaisAsync(ct);
        var saldos = (await erp.ConsultarAsync("TGFEST", "CODPROD", produto, ct))
            .Where(e => escopo.Filiais.Contains(e.Inteiro("CODEMP")))
            .Select(e =>
            {
                var disponivel = e.Inteiro("ESTOQUE") - e.Inteiro("RESERVADO");

                // Dado inconsistente não vai para o modelo como se fosse verdade
                return disponivel < 0
                    ? new EstoqueNaFilialDto(filiais[e.Inteiro("CODEMP")], 0,
                        "Reservado maior que o estoque físico no ERP. Confirme com o estoque antes de prometer prazo.")
                    : new EstoqueNaFilialDto(filiais[e.Inteiro("CODEMP")], disponivel, null);
            })
            .ToList();

        return new EstoqueDto(produto, cadastro.Texto("DESCRPROD"), saldos);
    }

    public async Task<SituacaoDoClienteDto?> SituacaoDoClienteAsync(Escopo escopo, int codigo, CancellationToken ct)
    {
        if (await erp.ConsultarAsync("TGFPAR", "CODPARC", codigo, ct) is not [var cliente, ..] || !escopo.PodeVerCliente(cliente))
            return null;

        var abertos = (await erp.ConsultarAsync("TGFFIN", "CODPARC", codigo, ct))
            .Where(t => t.Data("DHBAIXA") is null)
            .ToList();

        var vencidos = abertos.Where(t => t.Data("DTVENC") < Hoje).ToList();
        var limite = cliente.Valor("LIMCRED");
        var emAberto = abertos.Sum(t => t.Valor("VLRDESDOB"));

        return new SituacaoDoClienteDto(
            codigo,
            cliente.Texto("NOMEPARC"),
            limite,
            emAberto,
            vencidos.Sum(t => t.Valor("VLRDESDOB")),
            vencidos.Count,
            Math.Max(0, limite - emAberto));
    }

    public async Task<ResumoDeVendasDto> ResumoDeVendasAsync(Escopo escopo, int dias, CancellationToken ct)
    {
        var desde = Hoje.AddDays(-dias);
        var pedidos = (await PedidosAsync(escopo, ct))
            .Where(p => p.Situacao != "cancelado" && p.Emissao >= desde)
            .ToList();

        var total = pedidos.Sum(p => p.Valor);
        decimal? margem = null;

        if (escopo.VeMargem && total > 0)
            margem = Math.Round(pedidos.Sum(p => p.Valor * p.MargemPercentual!.Value / 100) / total * 100, 1);

        return new ResumoDeVendasDto(
            escopo.Descricao,
            dias,
            pedidos.Count,
            total,
            pedidos.Count == 0 ? 0 : Math.Round(total / pedidos.Count, 2),
            pedidos.Count(p => p.Atrasado),
            margem);
    }

    private async Task<IReadOnlyList<PedidoDto>> MontarPedidosAsync(Escopo escopo, IReadOnlyList<LinhaErp> linhas, CancellationToken ct)
    {
        var filiais = await NomesDasFiliaisAsync(ct);
        var clientes = (await erp.ConsultarAsync("TGFPAR", ct: ct))
            .ToDictionary(c => c.Inteiro("CODPARC"), c => c.Texto("NOMEPARC"));

        return [.. linhas.Select(p =>
        {
            var previsao = p.Data("DTPREVENT");
            var entregue = p.Data("DTENTREGA");
            var situacao = (p.Texto("STATUSNOTA"), entregue) switch
            {
                ("C", _) => "cancelado",
                (_, not null) => "entregue",
                ("L", _) => "faturado",
                _ => "aberto",
            };

            var atrasado = situacao is "aberto" or "faturado" && previsao < Hoje;
            var valor = p.Valor("VLRNOTA");

            decimal? margem = escopo.VeMargem && valor > 0
                ? Math.Round((valor - p.Valor("VLRCUSTO")) / valor * 100, 1)
                : null;

            return new PedidoDto(
                p.Inteiro("NUNOTA"),
                filiais[p.Inteiro("CODEMP")],
                p.Inteiro("CODPARC"),
                clientes.GetValueOrDefault(p.Inteiro("CODPARC"), "(cliente não cadastrado)"),
                p.Data("DTNEG")!.Value,
                previsao,
                entregue,
                situacao,
                atrasado,
                atrasado ? Hoje.DayNumber - previsao!.Value.DayNumber : null,
                valor,
                margem);
        })];
    }

    private async Task<Dictionary<int, string>> NomesDasFiliaisAsync(CancellationToken ct) =>
        (await erp.ConsultarAsync("TSIEMP", ct: ct)).ToDictionary(e => e.Inteiro("CODEMP"), e => e.Texto("NOMEFANTASIA"));
}
