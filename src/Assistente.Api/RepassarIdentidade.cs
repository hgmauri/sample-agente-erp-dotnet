namespace Assistente.Api;

/// <summary>
/// Toda chamada do agente à API de dados leva a identidade de quem perguntou.
/// O agente não tem acesso próprio: ele pode, no máximo, o que o usuário pode.
/// Em produção, troque o repasse do token pelo fluxo on-behalf-of do Entra ID
/// (Microsoft.Identity.Web), pedindo um token com a audiência da API de dados.
/// </summary>
public sealed class RepassarIdentidade(IHttpContextAccessor acessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var autorizacao = acessor.HttpContext?.Request.Headers.Authorization.ToString();

        // Sem usuário, sem consulta. Nada de "conta de serviço do agente" como plano B.
        if (string.IsNullOrEmpty(autorizacao))
            throw new InvalidOperationException("Chamada à API de dados sem a identidade do usuário.");

        request.Headers.TryAddWithoutValidation("Authorization", autorizacao);
        request.Headers.Add("X-Origem", "assistente");
        return base.SendAsync(request, cancellationToken);
    }
}
