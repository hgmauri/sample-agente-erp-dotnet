using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Comum;

/// <summary>
/// Autenticação FALSA, só para o exemplo rodar sem configurar nada.
/// O token "demo-ana" vira o usuário "ana". Em produção: Entra ID com JwtBearer
/// (ou Microsoft.Identity.Web) e, no assistente, o fluxo on-behalf-of.
/// </summary>
public sealed class AutenticacaoDeDemo(
    IOptionsMonitor<AuthenticationSchemeOptions> opcoes, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(opcoes, logger, encoder)
{
    public const string Esquema = "Demo";

    private static readonly Dictionary<string, string> Tokens = new(StringComparer.Ordinal)
    {
        ["demo-ana"] = "ana",
        ["demo-bruno"] = "bruno",
        ["demo-carla"] = "carla",
    };

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cabecalho = Request.Headers.Authorization.ToString();
        if (!cabecalho.StartsWith("Bearer ", StringComparison.Ordinal))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Tokens.TryGetValue(cabecalho["Bearer ".Length..].Trim(), out var usuario))
            return Task.FromResult(AuthenticateResult.Fail("Token inválido."));

        var identidade = new ClaimsIdentity([new Claim(ClaimTypes.Name, usuario)], Esquema);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identidade), Esquema)));
    }
}

public static class AutenticacaoDeDemoExtensions
{
    public static IServiceCollection AddAutenticacaoDeDemo(this IServiceCollection services)
    {
        services.AddAuthentication(AutenticacaoDeDemo.Esquema)
            .AddScheme<AuthenticationSchemeOptions, AutenticacaoDeDemo>(AutenticacaoDeDemo.Esquema, null);
        services.AddAuthorization();
        return services;
    }
}
