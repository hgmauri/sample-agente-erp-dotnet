using System.Threading.RateLimiting;
using Assistente.Api;
using Assistente.Api.Infra;
using Comum;
using Microsoft.Extensions.AI;

// O assistente não tem credencial do ERP nem do banco. Ele só enxerga a API de dados,
// e enxerga com a identidade de quem está perguntando.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutenticacaoDeDemo();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<RepassarIdentidade>();

builder.Services.AddHttpClient<Ferramentas>(http =>
    {
        http.BaseAddress = new Uri(builder.Configuration["Dados:Url"]
            ?? throw new InvalidOperationException("Configure Dados:Url."));
        http.Timeout = TimeSpan.FromSeconds(15);
    })
    .AddHttpMessageHandler<RepassarIdentidade>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("assistente", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.Identity?.Name ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

// Sem Foundry:Endpoint, usa um modelo simulado (e obediente demais) para o exemplo rodar sem Azure
var endpoint = builder.Configuration["Foundry:Endpoint"];
builder.Services
    .AddChatClient(string.IsNullOrWhiteSpace(endpoint)
        ? new ChatClientSimulado()
        : FoundryChatClientFactory.Criar(endpoint, builder.Configuration["Foundry:Deployment"] ?? "gpt-5.4-mini"))
    .UseFunctionInvocation(configure: f => f.MaximumIterationsPerRequest = 4);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/saude", () => Results.Ok(new { status = "ok" }));

app.MapPost("/assistente", async (Pergunta pergunta, IChatClient chat, Ferramentas ferramentas, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(pergunta.Texto) || pergunta.Texto.Length > 1000)
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["texto"] = ["Envie uma pergunta com até 1000 caracteres."],
        });

    // Instruções fixas no system; a pergunta do usuário vai separada, como dado
    List<ChatMessage> mensagens =
    [
        new(ChatRole.System, PromptDoAssistente.Sistema),
        new(ChatRole.User, pergunta.Texto),
    ];

    var resposta = await chat.GetResponseAsync(mensagens, new ChatOptions
    {
        Tools = ferramentas.Todas(),
        MaxOutputTokens = 800,
    }, ct);

    // Transparência: quem pergunta vê quais consultas embasaram a resposta
    var consultas = resposta.Messages
        .SelectMany(m => m.Contents)
        .OfType<FunctionCallContent>()
        .Select(c => new ConsultaFeita(c.Name, c.Arguments))
        .ToList();

    return Results.Ok(new RespostaDoAssistente(resposta.Text, consultas));
})
.RequireAuthorization()
.RequireRateLimiting("assistente");

app.Run();
