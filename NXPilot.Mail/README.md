# NXPilot.Mail

Leitura de e-mail do **Outlook / Microsoft 365** (Microsoft Graph) e do **Gmail** (Gmail API), com camada de serviço.
É **somente leitura por construção**: o pacote não tem nenhum método para enviar, responder, apagar ou mover mensagens.
Não tem dependências.

## Camadas

| Pasta | Tipo | Papel |
|---|---|---|
| `Models/` | `MailSummary`, `MailMessage`, `MailQuery`, `HtmlText` | `MailMessage.ToAgentContext()` entrega o e-mail ao agente como **dado não confiável**, delimitado e com aviso. |
| `Auth/` | `OAuthPkceFlow`, `OAuthTokenProvider`, `IOAuthTokenStore` | Login de app desktop (RFC 8252): navegador, loopback em porta aleatória e PKCE S256. O token é renovado sozinho. |
| `Providers/` | `GraphMailProvider`, `GmailProvider` | Listar a caixa de entrada e abrir mensagens, com o corpo sempre em texto. |
| `Services/` | `IMailService` / `MailService` | O que o app e os agentes usam. |

## Pré-requisitos (registro do aplicativo)

- **Outlook:** registro de app no Entra ID, plataforma "Mobile and desktop applications", redirect `http://localhost`, permissão delegada `Mail.Read`. Use `OAuthClientConfig.Outlook(clientId, tenant)`.
- **Gmail:** projeto no Google Cloud com a Gmail API ativada e cliente OAuth do tipo "Desktop app". Use `OAuthClientConfig.Gmail(clientId, clientSecret)`. O escopo `gmail.readonly` é **restrito**: para uso próprio ou da empresa, basta cadastrar os usuários de teste; para distribuir publicamente, é preciso passar pela verificação do Google.

## Uso

```csharp
var tokens = new OAuthTokenProvider(new OAuthPkceFlow(OAuthClientConfig.Outlook(clientId, tenantId)), store);
await tokens.LoginAsync();                                   // abre o navegador uma vez
using var mail = MailService.Create(MailProvider.Outlook, tokens);

var unread = await mail.ListInboxAsync(new MailQuery { UnreadOnly = true, Max = 10 });
var paraOAgente = await mail.GetForAgentAsync(unread[0].Id);   // texto delimitado como dado
```
