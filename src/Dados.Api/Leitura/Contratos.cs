using System.Text.Json.Serialization;

namespace Dados.Api.Leitura;

// Contratos com nomes de negócio. Sem CNPJ, sem custo, sem observação interna:
// o que não é necessário para responder a pergunta não sai daqui.

public sealed record PedidoDto(
    int Numero,
    string Filial,
    int CodigoCliente,
    string Cliente,
    DateOnly Emissao,
    DateOnly? PrevisaoDeEntrega,
    DateOnly? EntregueEm,
    string Situacao,
    bool Atrasado,
    int? DiasDeAtraso,
    decimal Valor,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? MargemPercentual);

public sealed record Pagina<T>(IReadOnlyList<T> Itens, int NumeroDaPagina, int Tamanho, int Total);

public sealed record EstoqueDto(int Produto, string Descricao, IReadOnlyList<EstoqueNaFilialDto> Filiais);

public sealed record EstoqueNaFilialDto(
    string Filial,
    int Disponivel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Alerta);

public sealed record SituacaoDoClienteDto(
    int Codigo,
    string Cliente,
    decimal LimiteDeCredito,
    decimal EmAberto,
    decimal Vencido,
    int TitulosVencidos,
    decimal CreditoDisponivel);

public sealed record ResumoDeVendasDto(
    string Escopo,
    int Dias,
    int Pedidos,
    decimal ValorTotal,
    decimal TicketMedio,
    int Atrasados,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] decimal? MargemPercentual);
