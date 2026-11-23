using System.Globalization;

namespace Erp.Simulado;

/// <summary>
/// Dados de um ERP fictício, do jeito que eles costumam estar: nomes de coluna técnicos,
/// tudo em texto, decimal com vírgula, data em dd/MM/yyyy e algumas inconsistências reais.
/// As datas são relativas a hoje para o exemplo funcionar em qualquer dia.
/// </summary>
public sealed class BancoErp
{
    private readonly Dictionary<string, List<Dictionary<string, string?>>> _tabelas;

    public BancoErp(TimeProvider relogio)
    {
        var hoje = DateOnly.FromDateTime(relogio.GetLocalNow().Date);
        string D(int dias) => hoje.AddDays(dias).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        _tabelas = new(StringComparer.OrdinalIgnoreCase)
        {
            // Empresas (filiais)
            ["TSIEMP"] =
            [
                Linha(("CODEMP", "1"), ("NOMEFANTASIA", "VITORIA")),
                Linha(("CODEMP", "2"), ("NOMEFANTASIA", "SERRA")),
            ],

            // Vendedores
            ["TGFVEN"] =
            [
                Linha(("CODVEND", "10"), ("APELIDO", "ANA SOUZA"), ("CODEMP", "1")),
                Linha(("CODVEND", "20"), ("APELIDO", "BRUNO LIMA"), ("CODEMP", "2")),
            ],

            // Parceiros (clientes)
            ["TGFPAR"] =
            [
                Linha(("CODPARC", "101"), ("NOMEPARC", "MERCADO BOA VISTA LTDA"), ("CGC_CPF", "12345678000190"), ("CODVEND", "10"), ("LIMCRED", "50.000,00")),
                Linha(("CODPARC", "102"), ("NOMEPARC", "PADARIA CENTRAL ME"), ("CGC_CPF", "23456789000101"), ("CODVEND", "10"), ("LIMCRED", "8.000,00")),
                Linha(("CODPARC", "201"), ("NOMEPARC", "ATACADO SERRANO SA"), ("CGC_CPF", "34567890000112"), ("CODVEND", "20"), ("LIMCRED", "120.000,00")),
                Linha(("CODPARC", "202"), ("NOMEPARC", "DISTRIBUIDORA JACARAIPE LTDA"), ("CGC_CPF", "45678901000123"), ("CODVEND", "20"), ("LIMCRED", "40.000,00")),
            ],

            // Produtos
            ["TGFPRO"] =
            [
                Linha(("CODPROD", "3001"), ("DESCRPROD", "CAFE TORRADO 500G")),
                Linha(("CODPROD", "3002"), ("DESCRPROD", "ACUCAR CRISTAL 5KG")),
                Linha(("CODPROD", "3003"), ("DESCRPROD", "OLEO DE SOJA 900ML")),
            ],

            // Estoque por filial. CUSTOMED é sensível. Repare no 3002 em Vitória: reservado maior que o físico.
            ["TGFEST"] =
            [
                Linha(("CODEMP", "1"), ("CODPROD", "3001"), ("ESTOQUE", "1200"), ("RESERVADO", "300"), ("CUSTOMED", "14,20")),
                Linha(("CODEMP", "1"), ("CODPROD", "3002"), ("ESTOQUE", "80"), ("RESERVADO", "95"), ("CUSTOMED", "11,05")),
                Linha(("CODEMP", "1"), ("CODPROD", "3003"), ("ESTOQUE", "500"), ("RESERVADO", "0"), ("CUSTOMED", "5,90")),
                Linha(("CODEMP", "2"), ("CODPROD", "3001"), ("ESTOQUE", "40"), ("RESERVADO", "0"), ("CUSTOMED", "14,20")),
                Linha(("CODEMP", "2"), ("CODPROD", "3002"), ("ESTOQUE", "600"), ("RESERVADO", "50"), ("CUSTOMED", "11,05")),
            ],

            // Cabeçalho dos pedidos. STATUSNOTA: A = aberto, L = liberado/faturado, C = cancelado.
            // VLRCUSTO e AD_OBSINTERNA nunca deveriam chegar ao vendedor, muito menos ao modelo.
            ["TGFCAB"] =
            [
                Pedido("5001", "1", "101", "10", D(-10), D(-2), null, "L", "18.450,00", "13.900,00", null),
                Pedido("5002", "1", "102", "10", D(-3), D(4), null, "A", "2.310,90", "1.650,00", null),
                Pedido("5003", "2", "201", "20", D(-6), D(1), null, "L", "96.780,00", "71.200,00", "NEGOCIACAO ESPECIAL: desconto de 12% aprovado pela diretoria"),
                Pedido("5004", "2", "202", "20", D(-20), D(-12), D(-11), "L", "7.640,00", "5.400,00", null),
                Pedido("5005", "1", "101", "10", D(-25), D(-18), D(-17), "L", "11.200,00", "8.300,00", null),
                Pedido("5006", "1", "102", "10", D(-2), null, null, "C", "990,00", "700,00", "cancelado a pedido do cliente"),
            ],

            // Financeiro (títulos a receber). DHBAIXA vazio = em aberto.
            ["TGFFIN"] =
            [
                Linha(("NUFIN", "7001"), ("CODPARC", "102"), ("VLRDESDOB", "1.150,00"), ("DTVENC", D(-15)), ("DHBAIXA", null)),
                Linha(("NUFIN", "7002"), ("CODPARC", "102"), ("VLRDESDOB", "800,00"), ("DTVENC", D(10)), ("DHBAIXA", null)),
                Linha(("NUFIN", "7003"), ("CODPARC", "101"), ("VLRDESDOB", "18.450,00"), ("DTVENC", D(20)), ("DHBAIXA", null)),
                Linha(("NUFIN", "7004"), ("CODPARC", "201"), ("VLRDESDOB", "30.000,00"), ("DTVENC", D(-40)), ("DHBAIXA", null)),
                Linha(("NUFIN", "7005"), ("CODPARC", "101"), ("VLRDESDOB", "11.200,00"), ("DTVENC", D(-5)), ("DHBAIXA", D(-6))),
            ],
        };
    }

    public List<Dictionary<string, string?>>? Tabela(string nome) =>
        _tabelas.TryGetValue(nome, out var linhas) ? linhas : null;

    private static Dictionary<string, string?> Linha(params (string Campo, string? Valor)[] campos) =>
        campos.ToDictionary(c => c.Campo, c => c.Valor, StringComparer.Ordinal);

    private static Dictionary<string, string?> Pedido(string nunota, string codemp, string codparc, string codvend,
        string dtneg, string? dtprevent, string? dtentrega, string status, string valor, string custo, string? obs) =>
        Linha(("NUNOTA", nunota), ("CODEMP", codemp), ("CODPARC", codparc), ("CODVEND", codvend),
              ("DTNEG", dtneg), ("DTPREVENT", dtprevent), ("DTENTREGA", dtentrega), ("STATUSNOTA", status),
              ("VLRNOTA", valor), ("VLRCUSTO", custo), ("AD_OBSINTERNA", obs));
}
