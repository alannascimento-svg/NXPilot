// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using NXPilot.Desktop.ViewModels;
using static NXPilot.NxText;

namespace NXPilot.Desktop.Tutorial;

/// <summary>
/// Um passo do tour. <see cref="Target"/> é o x:Name do elemento da MainWindow que fica em
/// destaque (nulo = cartão centralizado, tela toda escurecida); <see cref="Tab"/> troca a aba
/// central antes de destacar; <see cref="Try"/> é a ação do botão "Faça agora" — a pessoa
/// aprende fazendo, na tela de verdade, não num print.
/// </summary>
public sealed record TourStep(
    string Title,
    string Body,
    string? Target = null,
    int? Tab = null,
    string? TryLabel = null,
    Func<MainViewModel, Task>? Try = null);

public sealed record Lesson(string Id, string Title, string Summary, int Minutes, IReadOnlyList<TourStep> Steps);

/// <summary>
/// Catálogo das lições básicas, na ordem em que fazem sentido. É só conteúdo, com cada texto
/// em português e inglês lado a lado (quem traduz vê os dois juntos); a mecânica do tour fica
/// no <see cref="TourOverlay"/>. Montado a cada acesso para seguir o idioma ativo.
/// </summary>
public static class LessonCatalog
{
    // Índices das abas centrais (MainTabs).
    private const int TabPlano = 0, TabFases = 1, TabDiff = 2, TabTerminal = 3, TabLog = 4, TabLab = 5;

    public static IReadOnlyList<Lesson> All => Build();

    private static Lesson[] Build() => new[]
    {
        new Lesson("tela", T("Conhecendo a tela", "Getting to know the screen"),
            T("As áreas do NXPilot e para que serve cada uma.", "The areas of NXPilot and what each one is for."), 2, new[]
        {
            new TourStep(T("Bem-vindo ao NXPilot", "Welcome to NXPilot"),
                T("Inteligência Artificial Automática, e não autônoma — porque você está no controle.\n\n" +
                  "O NXPilot coordena agentes de código — Claude Code e Codex — usando a assinatura que você já tem. " +
                  "Ele divide o trabalho em fases, registra cada ação num log de auditoria e pede a sua decisão quando uma ação é arriscada.\n\n" +
                  "Nesta lição você conhece as áreas da tela. Use Próximo (ou a seta →) para avançar.",
                  "Automatic, not autonomous AI — because you are in control.\n\n" +
                  "NXPilot coordinates coding agents — Claude Code and Codex — using the subscription you already have. " +
                  "It splits the work into phases, records every action in an audit log and asks for your decision when an action is risky.\n\n" +
                  "In this lesson you get to know the screen. Use Next (or the → key) to move on.")),
            new TourStep(T("Barra superior", "Top bar"),
                T("Mostra a tarefa em andamento (aqui, uma Story do Azure DevOps), qual classificador está protegendo você agora, " +
                  "o botão para simular um pedido de agente, o idioma (PT/EN) e a Ajuda, que traz você de volta a estas lições.",
                  "Shows the task in progress (here, an Azure DevOps Story), which classifier is protecting you right now, " +
                  "the button to simulate an agent request, the language (PT/EN) and Help, which brings you back to these lessons."),
                Target: "HeaderBar"),
            new TourStep(T("Agentes", "Agents"),
                T("Cada agente aparece com o que está fazendo e há quanto tempo. Um agente pode abrir subagentes — eles aparecem indentados abaixo dele.",
                  "Each agent shows what it is doing and for how long. An agent can start subagents — they appear indented below it."),
                Target: "AgentsList"),
            new TourStep(T("Artefatos", "Artifacts"),
                T("Tudo o que os agentes produzem fica guardado aqui: a spec, o plano em fases, as revisões e o registro de decisões.",
                  "Everything the agents produce is kept here: the spec, the phased plan, the reviews and the decision log."),
                Target: "ArtifactsList"),
            new TourStep(T("Abas de trabalho", "Work tabs"),
                T("Plano, Fases, Diff, Terminal, Log, Laboratório IA e Canais. As próximas lições passam por elas.",
                  "Plan, Phases, Diff, Terminal, Log, AI Lab and Channels. The next lessons walk through them."),
                Target: "MainTabs"),
            new TourStep(T("Decisões", "Decisions"),
                T("O painel mais importante: ações que um agente quer executar e que precisam da sua aprovação. " +
                  "Cada cartão mostra o risco e por que ele foi classificado assim.",
                  "The most important panel: actions an agent wants to run that need your approval. " +
                  "Each card shows the risk and why it was classified that way."),
                Target: "DecisionsPanel"),
            new TourStep(T("Verificação", "Verification"),
                T("Ao fim de cada fase, outro agente revisa o código contra o plano. Os achados vêm por gravidade: crítico, maior e menor.",
                  "At the end of each phase, another agent reviews the code against the plan. Findings come by severity: critical, major and minor."),
                Target: "VerificationPanel"),
            new TourStep(T("Barra de status", "Status bar"),
                T("Worktrees ativos, consumo de tokens, tamanho do log e o work item vinculado — o resumo da sessão num relance.",
                  "Active worktrees, token usage, log size and the linked work item — the session summary at a glance."),
                Target: "StatusBar"),
        }),

        new Lesson("fases", T("Plano e fases", "Plan and phases"),
            T("Como uma tarefa vira fases, arquivos e diff.", "How a task becomes phases, files and a diff."), 3, new[]
        {
            new TourStep(T("O plano", "The plan"),
                T("Toda tarefa começa por uma intenção e critérios de aceite. O fluxo é sempre: Spec → Fases → Execução → Verificação.",
                  "Every task starts with an intent and acceptance criteria. The flow is always: Spec → Phases → Execution → Verification."),
                Target: "PlanContent", Tab: TabPlano),
            new TourStep(T("As fases", "The phases"),
                T("O plano é quebrado em fases pequenas, cada uma com um agente responsável e um worktree próprio — dois agentes nunca mexem na mesma cópia do código. " +
                  "Clique numa fase para ver o plano dela.",
                  "The plan is split into small phases, each with an owner agent and its own worktree — two agents never touch the same copy of the code. " +
                  "Click a phase to see its plan."),
                Target: "PhaseList", Tab: TabFases),
            new TourStep(T("Plano por arquivo", "Per-file plan"),
                T("Antes de escrever código, o agente diz quais arquivos vai criar ou alterar e por quê. É isso que a verificação usa depois para conferir.",
                  "Before writing code, the agent states which files it will create or change and why. That is what the verification checks against later."),
                Target: "PhaseFiles", Tab: TabFases),
            new TourStep("Diff",
                T("O que o agente realmente mudou, linha a linha: verde é adicionado, vermelho é removido.",
                  "What the agent actually changed, line by line: green is added, red is removed."),
                Target: "DiffView", Tab: TabDiff),
            new TourStep("Terminal",
                T("A saída ao vivo do agente. Repare nas linhas [NXPilot]: é o gate interceptando cada ferramenta antes de ela rodar.",
                  "The agent's live output. Note the [NXPilot] lines: that is the gate intercepting each tool before it runs."),
                Target: "TerminalView", Tab: TabTerminal),
        }),

        new Lesson("decisoes", T("Decidindo ações dos agentes", "Deciding agent actions"),
            T("Aprovar, negar e o que é aprovado sozinho.", "Approve, deny and what gets approved automatically."), 3, new[]
        {
            new TourStep(T("Um pedido de decisão", "A decision request"),
                T("Cada cartão traz: o agente, a ação exata (comando ou arquivo), o contexto da fase e o risco — com a confiança e o motivo.",
                  "Each card shows: the agent, the exact action (command or file), the phase context and the risk — with confidence and reason."),
                Target: "DecisionsPanel"),
            new TourStep(T("Faça você: um agente pede algo", "Your turn: an agent asks for something"),
                T("Clique em \"Faça agora\" (ou no botão Simular da barra) e veja um novo pedido chegar ao painel de decisões já classificado.",
                  "Click \"Do it now\" (or the Simulate button on the bar) and watch a new request arrive in the decisions panel, already classified."),
                Target: "SimulateButton", TryLabel: T("Faça agora: simular pedido", "Do it now: simulate a request"),
                Try: vm => vm.SimulateAgentActionCommand.ExecuteAsync(null)),
            new TourStep(T("Aprovar ou negar", "Approve or deny"),
                T("Risco MÉDIO ou ALTO sempre espera por você. Aprove para o agente continuar ou negue para bloquear — " +
                  "a área destacada continua clicável durante o tour.",
                  "MEDIUM or HIGH risk always waits for you. Approve to let the agent continue or deny to block it — " +
                  "the highlighted area stays clickable during the tour."),
                Target: "DecisionsPanel"),
            new TourStep(T("Aprovado sozinho", "Approved automatically"),
                T("Só o que é BAIXO com confiança (ler arquivo, git status, rodar testes) passa sem perguntar — e mesmo assim fica no log.",
                  "Only confident LOW (reading a file, git status, running tests) goes through without asking — and it is still logged."),
                Target: "DecisionsPanel"),
            new TourStep(T("Log de eventos", "Event log"),
                T("Toda ação, classificação e decisão — sua ou automática — fica registrada aqui: quem, o quê, quando e por quê. É a trilha de auditoria.",
                  "Every action, classification and decision — yours or automatic — is recorded here: who, what, when and why. It is the audit trail."),
                Target: "LogView", Tab: TabLog),
        }),

        new Lesson("tarefa", T("Sua primeira tarefa real", "Your first real task"),
            T("Do pedido ao diff: plano, fases, gate, verificação e Histórico.", "From request to diff: plan, phases, gate, verification and History."), 4, new[]
        {
            new TourStep(T("Comece por aqui", "Start here"),
                T("Nova tarefa abre o assistente: escolha um repositório git, descreva a intenção e os critérios de aceite, " +
                  "o modelo do Claude e quem revisa (Codex, Claude ou ninguém). Use um repositório de teste na primeira vez.",
                  "New task opens the wizard: pick a git repository, describe the intent and the acceptance criteria, " +
                  "the Claude model and who reviews (Codex, Claude or nobody). Use a test repository the first time."),
                Target: "NewTaskButton"),
            new TourStep(T("Nada muda na sua pasta", "Nothing changes in your folder"),
                T("O NXPilot cria um git worktree só para a tarefa, num branch nxpilot/<data>. O agente trabalha lá; " +
                  "a sua pasta de trabalho não é tocada. Juntar o resultado (merge/PR) continua sendo decisão sua.",
                  "NXPilot creates a git worktree just for the task, on an nxpilot/<date> branch. The agent works there; " +
                  "your working folder is not touched. Merging the result (merge/PR) remains your decision.")),
            new TourStep(T("O plano vem antes", "The plan comes first"),
                T("O Claude primeiro só LÊ o repositório e propõe um plano em fases, com os arquivos de cada fase. " +
                  "O plano aparece aqui, na aba Plano, com os botões Aprovar e Descartar. Sem aprovação, nada é escrito.",
                  "Claude first only READS the repository and proposes a phased plan, with each phase's files. " +
                  "The plan shows up here, in the Plan tab, with Approve and Discard buttons. Without approval, nothing is written."),
                Target: "PlanContent", Tab: TabPlano),
            new TourStep(T("Uma fase de cada vez", "One phase at a time"),
                T("Aprovado o plano, o agente executa fase por fase. Ao fim de cada fase o NXPilot faz um commit no worktree — " +
                  "um ponto de restauração por fase. Aqui você acompanha o status e o plano por arquivo de cada uma.",
                  "Once the plan is approved, the agent runs phase by phase. At the end of each phase NXPilot commits in the worktree — " +
                  "a restore point per phase. Here you follow each phase's status and per-file plan."),
                Target: "PhaseList", Tab: TabFases),
            new TourStep(T("O gate continua valendo", "The gate still applies"),
                T("Toda ferramenta que o agente quiser usar passa pelo gate ANTES de rodar. Leituras seguem sozinhas; " +
                  "escrever, instalar ou rodar scripts aparece aqui e o agente fica parado até você decidir. " +
                  "Escrever fora do worktree é sempre ALTO.",
                  "Every tool the agent wants to use goes through the gate BEFORE it runs. Reads go through on their own; " +
                  "writing, installing or running scripts shows up here and the agent waits until you decide. " +
                  "Writing outside the worktree is always HIGH."),
                Target: "DecisionsPanel"),
            new TourStep(T("Verificação cruzada", "Cross-verification"),
                T("No fim, outro agente — o Codex, em modo somente leitura — revisa o diff contra o plano e os critérios. " +
                  "Os achados aparecem aqui por gravidade; com algum crítico, a tarefa avisa para revisar antes de usar.",
                  "At the end, another agent — Codex, in read-only mode — reviews the diff against the plan and the criteria. " +
                  "Findings show up here by severity; with any critical one, the task warns you to review before using it."),
                Target: "VerificationPanel"),
            new TourStep(T("O resultado", "The result"),
                T("O diff da tarefa inteira, desde o ponto de partida. É o que você leva para o seu repositório quando estiver satisfeito.",
                  "The diff of the whole task, from the starting point. It is what you bring into your repository when you are satisfied."),
                Target: "DiffView", Tab: TabDiff),
            new TourStep(T("Histórico", "History"),
                T("Cada tarefa fica guardada com plano, fases, verificação, custo e o log de auditoria. " +
                  "Pelo Histórico você reabre uma tarefa na tela ou remove o worktree dela quando não precisar mais.",
                  "Each task is kept with its plan, phases, verification, cost and audit log. " +
                  "From History you reopen a task on screen or remove its worktree when you no longer need it."),
                Target: "HistoryButton"),
        }),

        new Lesson("cascata", T("A cascata: como o gate decide", "The cascade: how the gate decides"),
            T("Regras → ONNX → LLM, do barato ao caro.", "Rules → ONNX → LLM, from cheap to expensive."), 3, new[]
        {
            new TourStep(T("Três degraus", "Three steps"),
                T("O NXPilot decide o risco em cascata:\n\n" +
                  "1. Regras — instantâneas, para os casos óbvios (rm -rf, git push --force).\n" +
                  "2. ONNX — um modelo pequeno que roda em milissegundos na CPU, para o que as regras não conhecem.\n" +
                  "3. LLM (LLaMA local ou Copilot/Azure) — só quando o ONNX fica em dúvida. É mais lento, mas acerta mais.\n\n" +
                  "Assim você paga o modelo caro apenas nos casos difíceis.",
                  "NXPilot decides risk as a cascade:\n\n" +
                  "1. Rules — instant, for the obvious cases (rm -rf, git push --force).\n" +
                  "2. ONNX — a small model that runs in milliseconds on the CPU, for what the rules don't know.\n" +
                  "3. LLM (local LLaMA or Copilot/Azure) — only when ONNX is unsure. Slower, but more accurate.\n\n" +
                  "This way you only pay for the expensive model in the hard cases.")),
            new TourStep(T("Qual classificador está ativo", "Which classifier is active"),
                T("A barra superior sempre mostra o gate em uso. Com um LLM disponível, a cascata completa é o padrão.",
                  "The top bar always shows the gate in use. With an LLM available, the full cascade is the default."),
                Target: "GateStatusText"),
            new TourStep(T("ONNX: CPU ou GPU", "ONNX: CPU or GPU"),
                T("CPU é o padrão e funciona em qualquer computador. A GPU é opcional — e, para este modelo pequeno, a CPU costuma ser até mais rápida.",
                  "CPU is the default and works on any computer. The GPU is optional — and for this small model the CPU is often even faster."),
                Target: "LabOnnxCard", Tab: TabLab),
            new TourStep(T("O LLM da dúvida", "The LLM for doubtful cases"),
                T("O 3º degrau pode ser a IA Local (LLaMA, nada sai da máquina) ou um provedor da empresa: " +
                  "GitHub Copilot, Azure OpenAI, Microsoft 365 ou OpenRouter — em \"Trocar provedor…\". A caixa liga ou desliga o 3º degrau.",
                  "Step 3 can be the Local AI (LLaMA, nothing leaves the machine) or a company provider: " +
                  "GitHub Copilot, Azure OpenAI, Microsoft 365 or OpenRouter — in \"Change provider…\". The checkbox turns step 3 on or off."),
                Target: "LabLlamaCard", Tab: TabLab),
            new TourStep(T("Faça você: comparar os motores", "Your turn: compare the engines"),
                T("A comparação roda as mesmas ações em cada motor e mostra velocidade e acertos lado a lado. " +
                  "\"Faça agora\" roda a versão rápida (sem o LLaMA), em poucos segundos.",
                  "The comparison runs the same actions on each engine and shows speed and accuracy side by side. " +
                  "\"Do it now\" runs the quick version (without LLaMA) in a few seconds."),
                Target: "LabCompare", Tab: TabLab, TryLabel: T("Faça agora: comparar", "Do it now: compare"),
                Try: vm => { vm.Lab.BenchLlama = false; return vm.Lab.RunBenchmarkCommand.ExecuteAsync(null); }),
        }),

        new Lesson("ensinar", T("Ensinando o classificador", "Teaching the classifier"),
            T("Corrigir um erro e ver o gate aprender na hora.", "Fix a mistake and watch the gate learn right away."), 2, new[]
        {
            new TourStep(T("Testar uma ação", "Test an action"),
                T("Escreva qualquer comando e clique em Classificar: cada motor mostra o risco, a confiança e o tempo.",
                  "Type any command and click Classify: each engine shows the risk, the confidence and the time."),
                Target: "LabTry", Tab: TabLab, TryLabel: T("Faça agora: classificar o exemplo", "Do it now: classify the example"),
                Try: vm => vm.Lab.TryClassifyCommand.ExecuteAsync(null)),
            new TourStep(T("Ensinar", "Teach"),
                T("Se o ONNX errou, diga o risco certo. A ação vira um exemplo na hora — sem re-treinar — e ações parecidas passam a ser classificadas do mesmo jeito.",
                  "If ONNX got it wrong, tell it the right risk. The action becomes an example right away — no retraining — and similar actions get classified the same way."),
                Target: "LabTeach", Tab: TabLab),
            new TourStep(T("O gate aprende com a equipe", "The gate learns from the team"),
                T("Na versão completa, cada decisão que você corrige no painel vira exemplo automaticamente. " +
                  "Com o tempo, o gate aprende a política de risco da sua equipe.\n\nVocê concluiu as lições básicas!",
                  "In the full version, every decision you correct in the panel automatically becomes an example. " +
                  "Over time, the gate learns your team's risk policy.\n\nYou have completed the basic lessons!")),
        }),
    };
}
