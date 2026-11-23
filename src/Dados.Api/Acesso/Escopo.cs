using System.Security.Claims;
using Dados.Api.Erp;

namespace Dados.Api.Acesso;

public enum Perfil { Vendedor, Gerente }

/// <summary>O que este usuário pode ver no ERP. Calculado no servidor, nunca vindo do cliente ou do modelo.</summary>
public sealed record Escopo(string Usuario, Perfil Perfil, int? CodigoVendedor, IReadOnlySet<int> Filiais)
{
    public bool VeMargem => Perfil == Perfil.Gerente;

    public bool PodeVer(LinhaErp registro) =>
        Filiais.Contains(registro.Inteiro("CODEMP"))
        && (CodigoVendedor is null || registro.Inteiro("CODVEND") == CodigoVendedor);

    public bool PodeVerCliente(LinhaErp cliente) =>
        CodigoVendedor is null || cliente.Inteiro("CODVEND") == CodigoVendedor;

    public string Descricao => CodigoVendedor is null ? "todas as filiais" : $"carteira de {Usuario}";
}

/// <summary>
/// Vínculo entre o usuário da empresa e o cadastro dele no ERP.
/// Em produção: uma tabela mantida pelo comercial, com o id do usuário no Entra ID.
/// </summary>
public sealed class MapaDeUsuarios
{
    private static readonly Dictionary<string, Escopo> Usuarios = new(StringComparer.Ordinal)
    {
        ["ana"] = new("ana", Perfil.Vendedor, CodigoVendedor: 10, Filiais: new HashSet<int> { 1 }),
        ["bruno"] = new("bruno", Perfil.Vendedor, CodigoVendedor: 20, Filiais: new HashSet<int> { 2 }),
        ["carla"] = new("carla", Perfil.Gerente, CodigoVendedor: null, Filiais: new HashSet<int> { 1, 2 }),
    };

    // Usuário autenticado mas sem vínculo no ERP não vê nada (fail closed)
    public Escopo? Obter(ClaimsPrincipal usuario) =>
        usuario.Identity?.Name is { } nome && Usuarios.TryGetValue(nome, out var escopo) ? escopo : null;
}

public sealed class FiltroDeEscopo(MapaDeUsuarios mapa) : IEndpointFilter
{
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (mapa.Obter(context.HttpContext.User) is not { } escopo)
            return ValueTask.FromResult<object?>(Results.Forbid());

        context.HttpContext.Items[typeof(Escopo)] = escopo;
        return next(context);
    }
}

public static class EscopoExtensions
{
    public static Escopo Escopo(this HttpContext contexto) =>
        contexto.Items[typeof(Escopo)] as Escopo
        ?? throw new InvalidOperationException("Escopo não resolvido. O endpoint está fora do grupo /v1?");
}
