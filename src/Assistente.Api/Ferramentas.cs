using System.ComponentModel;
using System.Globalization;
using System.Net;
using Microsoft.Extensions.AI;

namespace Assistente.Api;

/// <summary>
/// As ferramentas do agente são finas: cada uma chama UM endpoint da API de dados.
/// Nenhuma recebe SQL, nome de tabela, id de usuário ou vendedor. Todas são só leitura.
/// </summary>
public sealed class Ferramentas(HttpClient http)
{
    public IList<AITool> Todas() =>
    [
        AIFunctionFactory.Create(ListarPedidosAsync, "listar_pedidos",
            "Lista os pedidos que o usuário pode ver, do mais recente para o mais antigo."),
        AIFunctionFactory.Create(ConsultarPedidoAsync, "consultar_pedido",
            "Consulta um pedido pelo número: cliente, situação, previsão de entrega, atraso e valor."),
        AIFunctionFactory.Create(ConsultarEstoqueAsync, "consultar_estoque",
            "Consulta o saldo disponível de um produto nas filiais do usuário."),
        AIFunctionFactory.Create(SituacaoDoClienteAsync, "situacao_do_cliente",
            "Consulta limite de crédito, valores em aberto e títulos vencidos de um cliente da carteira do usuário."),
        AIFunctionFactory.Create(ResumoDeVendasAsync, "resumo_de_vendas",
            "Totais de vendas já calculados (quantidade, valor, ticket médio, atrasados) para os últimos N dias."),
    ];

    private Task<string> ListarPedidosAsync(
        [Description("todos, abertos ou atrasados")] string situacao = "todos",
        [Description("Página, começando em 1")] int pagina = 1,
        CancellationToken ct = default) =>
        ObterAsync(Url($"v1/pedidos?situacao={Uri.EscapeDataString(situacao)}&pagina={pagina}&tamanho=20"), ct);

    private Task<string> ConsultarPedidoAsync([Description("Número do pedido")] int numero, CancellationToken ct = default) =>
        ObterAsync(Url($"v1/pedidos/{numero}"), ct);

    private Task<string> ConsultarEstoqueAsync([Description("Código do produto")] int produto, CancellationToken ct = default) =>
        ObterAsync(Url($"v1/estoque/{produto}"), ct);

    private Task<string> SituacaoDoClienteAsync([Description("Código do cliente")] int cliente, CancellationToken ct = default) =>
        ObterAsync(Url($"v1/clientes/{cliente}/situacao"), ct);

    private Task<string> ResumoDeVendasAsync([Description("Quantidade de dias, de 1 a 90")] int dias = 30, CancellationToken ct = default) =>
        ObterAsync(Url($"v1/vendas/resumo?dias={dias}"), ct);

    private async Task<string> ObterAsync(string url, CancellationToken ct)
    {
        using var resposta = await http.GetAsync(new Uri(url, UriKind.Relative), ct);

        // O modelo recebe um resultado claro, nunca detalhes internos do erro
        return resposta.StatusCode switch
        {
            HttpStatusCode.OK => await resposta.Content.ReadAsStringAsync(ct),
            HttpStatusCode.NotFound or HttpStatusCode.Forbidden =>
                """{"resultado":"nao_encontrado","mensagem":"Não encontrado ou sem acesso para este usuário."}""",
            HttpStatusCode.BadRequest => """{"resultado":"parametro_invalido"}""",
            _ => """{"resultado":"indisponivel","mensagem":"Consulta indisponível no momento."}""",
        };
    }

    private static string Url(FormattableString url) => url.ToString(CultureInfo.InvariantCulture);
}
