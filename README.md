# NXPilot

> **Inteligência Artificial Automática, e não autônoma — porque você está no controle.**
> *Automatic, not autonomous AI — because you are in control.*

O NXPilot é um app desktop (Windows) que coordena agentes de código — **Claude Code** e **Codex** —
registra tudo para auditoria e **pede a sua decisão** antes de qualquer ação arriscada.

## Como funciona

1. **Nova tarefa** — você descreve a intenção e os critérios de aceite; o agente só LÊ o repositório e propõe um plano em fases.
2. **Você aprova** — nada é escrito antes disso, e tudo roda num *git worktree* isolado (sua pasta de trabalho não é tocada).
3. **Execução sob o gate** — cada comando ou arquivo que o agente quer usar passa pela **cascata de risco**:
   **Regras → ONNX (local, ~2 ms) → LLM só na dúvida**. Só ações de risco BAIXO com confiança alta passam sozinhas;
   MÉDIO e ALTO sempre vêm para você. Escrever fora do worktree é sempre ALTO.
4. **Commit por fase** e **verificação cruzada**: o outro agente revisa o diff (somente leitura) contra o plano e os critérios.

Também: histórico de tarefas, notificações, work items do Azure DevOps (somente leitura), projetos Power BI (PBIP)
editados como código, leitura de mensagens pela API oficial do WhatsApp (somente leitura), IA local (LLaMA) ou
provedores (GitHub Copilot/Models, Azure OpenAI, Microsoft 365, OpenRouter), interface em português e inglês.

## Edições (Open Core)

O **Open Core abrange somente o NXPilot**. O **NXAgentic-Provider** é a versão comercial (Enterprise),
para empresas que querem aproveitar melhor o uso de **skills corporativas** — não é Open Core e seu
código não é publicado.

| | **NXPilot Community** (este repositório) | **NXAgentic-Provider** (comercial / Enterprise) |
|---|---|---|
| Preço | gratuito, inclusive para empresas (uso interno) | licença paga |
| Agentes, plano em fases, worktree, verificação cruzada | ✔ | ✔ |
| **Gate, cascata de risco, log e auditoria local** | ✔ — segurança nunca é recurso pago | ✔ |
| Planejador | Basic | avançado (roteamento de modelos, avaliação do plano) |
| Skills corporativas, políticas, equipe, relatórios | — | ✔ |

O NXPilot funciona completo sem o NXAgentic-Provider. A versão comercial se conecta pela fachada
pública `IAgentPlanner` (`NXPilot.Core/Planning`); um módulo local só é carregado se estiver
**assinado pela Nexus XData**, e qualquer falha faz o NXPilot seguir com o planejador Basic.
Sem o NXAgentic-Provider, nenhum código, prompt ou contexto seu é enviado à Nexus XData, e não há
telemetria. Mesmo com ele, o serviço só **sugere**: toda ação dos agentes continua passando pelo
gate local e por você.

## Requisitos

- Windows 10/11 x64 (o Setup já traz o runtime .NET).
- Pelo menos um agente instalado e logado: [Claude Code](https://claude.com/claude-code) ou Codex.
- `git` no PATH.
- Opcional: modelo ONNX em `C:\IA_LOCAL\ONNX` (o app baixa) e IA local em `C:\IA_LOCAL`.

## Instalação

Baixe o `NXPilot-Setup.zip` da última *release*, extraia e rode o `NXPilot-Setup.exe`.

## Desenvolvimento

```powershell
.\build-nxpilot.ps1                 # compila + smoke test + testes críticos
.\run-nxpilot.ps1                   # abre o app já compilado
.\release-nxpilot-setup.ps1         # gera dist\setup\NXPilot-Setup.zip
.\release-nxpilot-new-version.ps1   # nova versão: testes, versão, Setup, commit, tag, release
```

Testes críticos: `NXPilot.Bench.exe --test [filtro]`.

## Licença

**NXPilot License 2.0** — Open Core / licenciamento dual, *source-available* (não é uma licença OSI).
Uso gratuito, inclusive comercial interno; distribuição comercial somente com contrato.
Texto oficial: [LICENSE.txt](LICENSE.txt) · English: [LICENSE.en.txt](LICENSE.en.txt) ·
Terceiros: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

Copyright (c) Nexus XData Tecnologia Ltda · comercial.nexus.xdata@gmail.com
