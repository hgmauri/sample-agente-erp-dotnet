using System.Globalization;

namespace Dados.Api.Erp;

/// <summary>Cliente da API do ERP. Usa a chave de serviço, que nunca sai desta aplicação.</summary>
public sealed class ClienteErp(HttpClient http)
{
    public async Task<IReadOnlyList<LinhaErp>> ConsultarAsync(
        string tabela, IReadOnlyDictionary<string, string>? filtros = null, CancellationToken ct = default)
    {
        var url = "erp/consulta/" + Uri.EscapeDataString(tabela);
        if (filtros is { Count: > 0 })
            url += "?" + string.Join("&", filtros.Select(f => $"{Uri.EscapeDataString(f.Key)}={Uri.EscapeDataString(f.Value)}"));

        var resposta = await http.GetFromJsonAsync<RespostaErp>(url, ct)
            ?? throw new InvalidOperationException("Resposta vazia do ERP.");

        return [.. resposta.Linhas.Select(l => new LinhaErp(l))];
    }

    public Task<IReadOnlyList<LinhaErp>> ConsultarAsync(string tabela, string campo, int valor, CancellationToken ct = default) =>
        ConsultarAsync(tabela, new Dictionary<string, string> { [campo] = valor.ToString(CultureInfo.InvariantCulture) }, ct);

    private sealed record RespostaErp(List<Dictionary<string, string?>> Linhas);
}

/// <summary>
/// Uma linha crua do ERP. Toda a conversão de formato (decimal com vírgula, data dd/MM/yyyy,
/// campo vazio) acontece aqui, uma vez, e não no prompt.
/// </summary>
public sealed class LinhaErp(Dictionary<string, string?> campos)
{
    public string Texto(string campo) => campos.GetValueOrDefault(campo)?.Trim() ?? "";

    public int Inteiro(string campo) => int.Parse(Texto(campo), CultureInfo.InvariantCulture);

    // "18.450,00" -> 18450.00
    public decimal Valor(string campo) =>
        decimal.Parse(Texto(campo).Replace(".", "", StringComparison.Ordinal).Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture);

    public DateOnly? Data(string campo) =>
        Texto(campo) is { Length: > 0 } texto
            ? DateOnly.ParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture)
            : null;
}
