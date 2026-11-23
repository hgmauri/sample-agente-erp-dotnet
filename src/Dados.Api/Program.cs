using Comum;
using Dados.Api.Acesso;
using Dados.Api.Auditoria;
using Dados.Api.Erp;
using Dados.Api.Leitura;

// A API de dados é a única coisa que fala com o ERP.
// Ela sabe quem é o usuário, o que ele pode ver e devolve dados já limpos, com nomes de negócio.
// O agente (e qualquer outro sistema) consome esta API, nunca o ERP.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAutenticacaoDeDemo();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<MapaDeUsuarios>();
builder.Services.AddSingleton<RegistroDeAuditoria>();
builder.Services.AddScoped<LeituraComercial>();

// A chave de serviço do ERP fica SÓ aqui. Em produção: Key Vault + Managed Identity.
builder.Services.AddHttpClient<ClienteErp>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Erp:Url"]
        ?? throw new InvalidOperationException("Configure Erp:Url."));
    http.DefaultRequestHeaders.Add("X-Erp-Chave", builder.Configuration["Erp:ChaveDeServico"]
        ?? throw new InvalidOperationException("Configure Erp:ChaveDeServico."));
    http.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<MiddlewareDeAuditoria>();

app.MapGet("/saude", () => Results.Ok(new { status = "ok" }));

var v1 = app.MapGroup("/v1")
    .RequireAuthorization()
    .AddEndpointFilter<FiltroDeEscopo>();

v1.MapConsultasComerciais();

v1.MapGet("/auditoria", (HttpContext ctx, RegistroDeAuditoria auditoria) =>
    ctx.Escopo().Perfil == Perfil.Gerente
        ? Results.Ok(auditoria.Ultimos(200))
        : Results.Forbid());

app.Run();
