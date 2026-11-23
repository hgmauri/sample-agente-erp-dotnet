using System.Security.Cryptography;
using System.Text;
using Erp.Simulado;

// Simula a API de um ERP típico: tabelas com nomes técnicos, valores em texto,
// datas em dd/MM/yyyy, códigos de status e campos internos (custo, observação interna).
// Só aceita uma chave de serviço. Ela dá acesso a TUDO, de todas as filiais.
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var chave = Encoding.UTF8.GetBytes(app.Configuration["Erp:ChaveDeServico"]
    ?? throw new InvalidOperationException("Configure Erp:ChaveDeServico."));
var banco = new BancoErp(TimeProvider.System);

// GET /erp/consulta/TGFCAB?CODVEND=10  (filtros de igualdade por campo, como muitas APIs de ERP)
app.MapGet("/erp/consulta/{tabela}", (string tabela, HttpRequest request) =>
{
    var recebida = Encoding.UTF8.GetBytes(request.Headers["X-Erp-Chave"].ToString());
    if (!CryptographicOperations.FixedTimeEquals(recebida, chave))
        return Results.Unauthorized();

    var linhas = banco.Tabela(tabela);
    if (linhas is null)
        return Results.NotFound();

    foreach (var (campo, valor) in request.Query)
        linhas = linhas.Where(l => l.TryGetValue(campo.ToUpperInvariant(), out var v) && v == valor.ToString()).ToList();

    return Results.Ok(new { tabela = tabela.ToUpperInvariant(), total = linhas.Count, linhas });
});

app.Run();
