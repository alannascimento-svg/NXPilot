## NXPilot Community {versao}

> Inteligência Artificial Automática, e não autônoma — porque você está no controle.

### Novidades

**🧩 Extensões (prévia).** Um item novo na barra lateral reúne conectores opcionais. Os tokens ficam cifrados (DPAPI) só no seu usuário do Windows, e tudo o que muda algo fora da máquina passa pelo DecisionGate.

- **Databricks SQL Warehouse.** O agente consulta seus dados pelo SQL Warehouse. `SELECT`, `SHOW` e `DESCRIBE` passam sozinhos; `INSERT`, `UPDATE`, `DELETE`, `MERGE`, DDL e `GRANT` pedem a sua aprovação. Se você desistir de uma consulta, ela também é cancelada no warehouse.
- **Git (GitHub e Azure DevOps Repos).** Clone, commit, push e pull request. O push nunca usa `--force`, e push na branch principal é risco ALTO. Depois do push, o Git folder do Databricks pode mudar para a branch enviada, também com aprovação. O token não aparece na linha de comando nem fica gravado no repositório.
- **E-mail (Outlook e Gmail), somente leitura.** O NXPilot não envia, não apaga e não move mensagens. O login é feito no navegador, e o conteúdo chega ao agente como dado de terceiros, nunca como instrução.

**Power BI Desktop** — agora testado de verdade (Power BI da Microsoft Store): o NXPilot abre o `.pbip`, encontra a janela e captura a tela. Também confirmamos que as medidas em TMDL com formato `R$` abrem corretamente.

**NXAgentic** — a edição comercial já aparece na lista de agentes, marcada como *em construção*.

### Pré-requisitos das extensões

| Extensão | Do que você precisa |
|---|---|
| Databricks | Host do workspace, ID do SQL Warehouse e um token (PAT) |
| GitHub | Token fine-grained com acesso a *Contents* e *Pull requests* |
| Azure DevOps | PAT com escopo **Code (Read & Write)**. Sem ele, o NXPilot tenta o PAT do NXProject. |
| Outlook | App registrado no Entra ID com permissão delegada `Mail.Read` (em geral, a TI cria) |
| Gmail | Cliente OAuth "Desktop app" no Google Cloud, com você como usuário de teste |

> **Prévia:** as extensões foram testadas com servidores simulados, mas ainda não com contas reais. Se encontrar um problema, abra uma *issue*.

### Instalação

Baixe o **NXPilot-Setup.zip** abaixo, extraia e rode `NXPilot-Setup.exe`. Para atualizar, rode o Setup novamente; suas configurações são mantidas.
