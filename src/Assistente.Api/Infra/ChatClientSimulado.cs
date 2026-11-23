using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace Assistente.Api.Infra;

/// <summary>
/// Modelo simulado para o exemplo rodar sem Azure. Ele é "obediente demais": se a pergunta
/// mandar consultar o pedido de outro vendedor, ele tenta. É de propósito: a segurança
/// não pode depender de o modelo se comportar.
/// </summary>
public sealed partial class ChatClientSimulado : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var lista = messages.ToList();

        // Segunda volta: a ferramenta respondeu, então o "modelo" responde com base no resultado
        if (lista[^1].Role == ChatRole.Tool)
        {
            var chamadas = lista.SelectMany(m => m.Contents).OfType<FunctionCallContent>().ToDictionary(c => c.CallId);
            var resultados = lista[^1].Contents.OfType<FunctionResultContent>()
                .Select(r => $"[{chamadas.GetValueOrDefault(r.CallId)?.Name}] {r.Result}");
            return Responder(new TextContent("Consultei o sistema. " + string.Join(" ", resultados)));
        }

        var pergunta = lista.Last(m => m.Role == ChatRole.User).Text;
        var ferramentas = options?.Tools?.OfType<AIFunction>().Select(f => f.Name).ToHashSet() ?? [];

        var (nome, argumentos) = Escolher(pergunta);
        if (nome is null || !ferramentas.Contains(nome))
            return Responder(new TextContent("Posso consultar pedidos, estoque, situação de clientes e resumo de vendas."));

        return Responder(new FunctionCallContent(Guid.NewGuid().ToString("N"), nome, argumentos));
    }

    private static (string? Nome, Dictionary<string, object?> Argumentos) Escolher(string pergunta)
    {
        if (Pedido().Match(pergunta) is { Success: true } pedido)
            return ("consultar_pedido", new() { ["numero"] = Numero(pedido) });

        if (pergunta.Contains("estoque", StringComparison.OrdinalIgnoreCase) && Produto().Match(pergunta) is { Success: true } produto)
            return ("consultar_estoque", new() { ["produto"] = Numero(produto) });

        if (Cliente().Match(pergunta) is { Success: true } cliente)
            return ("situacao_do_cliente", new() { ["cliente"] = Numero(cliente) });

        if (Vendas().IsMatch(pergunta))
            return ("resumo_de_vendas", new() { ["dias"] = Dias().Match(pergunta) is { Success: true } d ? Numero(d) : 30 });

        if (pergunta.Contains("atrasad", StringComparison.OrdinalIgnoreCase))
            return ("listar_pedidos", new() { ["situacao"] = "atrasados" });

        if (pergunta.Contains("pedidos", StringComparison.OrdinalIgnoreCase))
            return ("listar_pedidos", new() { ["situacao"] = pergunta.Contains("abert", StringComparison.OrdinalIgnoreCase) ? "abertos" : "todos" });

        return (null, []);
    }

    private static int Numero(Match m) => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private static Task<ChatResponse> Responder(AIContent conteudo) =>
        Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [conteudo])));

    [GeneratedRegex(@"pedido\D{0,20}?(\d{4,})", RegexOptions.IgnoreCase)]
    private static partial Regex Pedido();

    [GeneratedRegex(@"\b(\d{4})\b")]
    private static partial Regex Produto();

    [GeneratedRegex(@"cliente\D{0,20}?(\d{3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex Cliente();

    [GeneratedRegex(@"resumo|vendas|vendemos|vendi", RegexOptions.IgnoreCase)]
    private static partial Regex Vendas();

    [GeneratedRegex(@"(\d{1,2})\s*dias", RegexOptions.IgnoreCase)]
    private static partial Regex Dias();
}
