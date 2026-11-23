namespace Assistente.Api;

public sealed record Pergunta(string Texto);

public sealed record ConsultaFeita(string Ferramenta, IDictionary<string, object?>? Argumentos);

public sealed record RespostaDoAssistente(string Resposta, IReadOnlyList<ConsultaFeita> Consultas);
