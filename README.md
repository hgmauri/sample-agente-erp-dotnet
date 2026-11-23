# sample-agente-erp-dotnet

Um agente de IA que consulta o ERP **com segurança**, em **.NET 10** e **Microsoft Foundry**: o agente nunca fala com o ERP. Ele fala com uma **API de dados** que sabe quem é o usuário, o que ele pode ver e devolve dados limpos, com nomes de negócio.

Artigo completo: [Agente de IA que consulta o ERP com segurança: dados antes da IA na prática](https://henriquemauri.net/agente-de-ia-que-consulta-o-erp-com-seguranca/)

## Arquitetura

```
Usuário (token)
   │
   ▼
Assistente.Api  :5402   IChatClient (Foundry ou simulado) + 5 ferramentas só de leitura
   │  repassa a identidade do usuário (em produção: on-behalf-of)
   ▼
Dados.Api       :5400   escopo por usuário, contratos de negócio, contas no código, auditoria
   │  única dona da chave de serviço do ERP
   ▼
Erp.Simulado    :5401   tabelas cruas: TGFCAB, TGFPAR, TGFEST, TGFFIN...
```

| Projeto | O que faz |
|---------|-----------|
| `src/Erp.Simulado` | Simula a API de um ERP típico: nomes técnicos, decimal com vírgula, data `dd/MM/yyyy`, custo e observação interna, uma chave que vê tudo |
| `src/Dados.Api` | A camada que importa: mapeia usuário para vendedor e filial, filtra, traduz, calcula, esconde o que não é necessário e audita |
| `src/Assistente.Api` | O agente: prompt fixo, ferramentas finas que chamam a API de dados com a identidade do usuário, limites de tokens, iterações e requisições |
| `src/Comum` | Autenticação **falsa, só para a demo** (`Bearer demo-ana`, `demo-bruno`, `demo-carla`) |
| `src/Cenarios` | 14 cenários de segurança e qualidade de dado executados no CI |

## Usuários da demo

| Usuário | Perfil | Vê |
|---------|--------|----|
| `ana` | Vendedora, filial Vitória | Só a própria carteira, sem custo e sem margem |
| `bruno` | Vendedor, filial Serra | Só a própria carteira, sem custo e sem margem |
| `carla` | Gerente comercial | Todas as filiais, com margem e auditoria |

## Como rodar

```bash
./scripts/subir.sh            # Linux/macOS
# ou: ./scripts/subir.ps1     # Windows

# em outro terminal
dotnet run --project src/Cenarios
```

Saída esperada:

```
Cenário                                       Resultado  Detalhe
--------------------------------------------------------------------------------------------------------------
Vendedora consulta o próprio pedido           ok         cliente e atraso na resposta
Prompt injection: pedido de outro vendedor    ok         o modelo obedeceu e tentou; a API de dados negou
Custo e margem não chegam ao vendedor         ok         DTO do vendedor não tem esses campos
Gerente vê a margem                           ok         margem calculada na API
Total calculado na API, não no modelo         ok         gerente: todas as filiais; vendedora: só a carteira dela
Estoque só da filial do usuário               ok         bruno só vê a filial Serra
Dado inconsistente do ERP vem sinalizado      ok         saldo negativo vira 0 com alerta
Cliente de outra carteira                     ok         não confirma nem que o cliente existe
Sem token, sem assistente                     ok         HTTP 401
API de dados: pedido de outro vendedor        ok         HTTP 404
API de dados: página limitada a 50            ok         tamanho=50
ERP recusa chamada sem a chave de serviço     ok         HTTP 401
Auditoria registra a tentativa                ok         ana, via assistente, /v1/pedidos/5003, 404
Vendedor não lê a auditoria                   ok         HTTP 403

Todos os 14 cenários passaram.
```

Requisições prontas para testar na mão: [`docs/requisicoes.http`](docs/requisicoes.http).

## Usando o Microsoft Foundry

Sem `Foundry:Endpoint`, o assistente usa um **modelo simulado e obediente demais**: se a pergunta mandar consultar o pedido de outro vendedor, ele tenta. É de propósito: a segurança não pode depender de o modelo se comportar.

Para usar um modelo de verdade, faça `az login` e configure (o acesso é por Entra ID, sem chave de API):

```bash
cd src/Assistente.Api
dotnet user-secrets init
dotnet user-secrets set "Foundry:Endpoint" "https://<seu-recurso>.services.ai.azure.com/"
dotnet user-secrets set "Foundry:Deployment" "gpt-5.4-mini"
```

Sua conta precisa do papel **Azure AI User** no recurso do Foundry.

## Checklist: dados antes da IA

- [ ] O agente não tem credencial do ERP nem do banco. Só a API de dados tem.
- [ ] Toda chamada do agente leva a identidade do usuário (on-behalf-of), nunca uma conta de serviço "do agente".
- [ ] O vínculo usuário → vendedor/filial/perfil é resolvido no servidor, não vem do token, do corpo ou do modelo.
- [ ] Endpoints com propósito definido. Nada de SQL livre, nome de tabela como parâmetro ou "consulta genérica".
- [ ] Contratos com nomes de negócio. Campos que a pergunta não precisa (CNPJ, custo, observação interna) não saem.
- [ ] Datas, valores e códigos convertidos uma vez, no código. Dado inconsistente vem sinalizado.
- [ ] Totais, médias e margens calculados na API. O modelo não soma.
- [ ] Registro de outro usuário responde 404. Página e período têm limite.
- [ ] Toda consulta é auditada com usuário e origem (app ou assistente).
- [ ] Cenários automatizados provam que, mesmo com o modelo obedecendo a um ataque, o dado não sai.

## Licença

MIT

---

Feito por [Henrique Mauri](https://henriquemauri.net) | [Dotcode](https://dotcode.com.br)
