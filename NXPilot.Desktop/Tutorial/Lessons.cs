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
/// em português, inglês e espanhol lado a lado (quem traduz vê todos juntos); a mecânica do tour fica
/// no <see cref="TourOverlay"/>. Montado a cada acesso para seguir o idioma ativo.
/// </summary>
public static class LessonCatalog
{
    // Índices das abas centrais (MainTabs).
    private const int TabPlano = 0, TabFases = 1, TabDiff = 2, TabTerminal = 3, TabLog = 4, TabLab = 5;

    public static IReadOnlyList<Lesson> All => Build();

    private static Lesson[] Build() => new[]
    {
        new Lesson("tela", T("Conhecendo a tela", "Getting to know the screen", "Conociendo la pantalla"),
            T("As áreas do NXPilot e para que serve cada uma.", "The areas of NXPilot and what each one is for.",
              "Las áreas de NXPilot y para qué sirve cada una."), 2, new[]
        {
            new TourStep(T("Bem-vindo ao NXPilot", "Welcome to NXPilot", "Bienvenido a NXPilot"),
                T("Inteligência Artificial Automática, e não autônoma — porque você está no controle.\n\n" +
                  "O NXPilot coordena agentes de código — Claude Code e Codex — usando a assinatura que você já tem. " +
                  "Ele divide o trabalho em fases, registra cada ação num log de auditoria e pede a sua decisão quando uma ação é arriscada.\n\n" +
                  "Nesta lição você conhece as áreas da tela. Use Próximo (ou a seta →) para avançar.",
                  "Automatic, not autonomous AI — because you are in control.\n\n" +
                  "NXPilot coordinates coding agents — Claude Code and Codex — using the subscription you already have. " +
                  "It splits the work into phases, records every action in an audit log and asks for your decision when an action is risky.\n\n" +
                  "In this lesson you get to know the screen. Use Next (or the → key) to move on.",
                  "Inteligencia Artificial automática, no autónoma — usted tiene el control.\n\n" +
                  "NXPilot coordina agentes de código — Claude Code y Codex — usando la suscripción que usted ya tiene. " +
                  "Divide el trabajo en fases, registra cada acción en un log de auditoría y le pide su decisión cuando una acción es riesgosa.\n\n" +
                  "En esta lección usted conocerá las áreas de la pantalla. Use Siguiente (o la tecla →) para avanzar.")),
            new TourStep(T("Barra superior", "Top bar", "Barra superior"),
                T("Mostra a tarefa em andamento (aqui, uma Story do Azure DevOps), qual classificador está protegendo você agora, " +
                  "o botão para simular um pedido de agente, o idioma (PT/EN/ES) e a Ajuda, que traz você de volta a estas lições.",
                  "Shows the task in progress (here, an Azure DevOps Story), which classifier is protecting you right now, " +
                  "the button to simulate an agent request, the language (PT/EN/ES) and Help, which brings you back to these lessons.",
                  "Muestra la tarea en curso (aquí, una Story de Azure DevOps), qué clasificador lo está protegiendo ahora, " +
                  "el botón para simular una solicitud de agente, el idioma (PT/EN/ES) y la Ayuda, que lo trae de vuelta a estas lecciones."),
                Target: "HeaderBar"),
            new TourStep(T("Agentes", "Agents", "Agentes"),
                T("Cada agente aparece com o que está fazendo e há quanto tempo. Um agente pode abrir subagentes — eles aparecem indentados abaixo dele.",
                  "Each agent shows what it is doing and for how long. An agent can start subagents — they appear indented below it.",
                  "Cada agente aparece con lo que está haciendo y desde hace cuánto tiempo. Un agente puede abrir subagentes — aparecen con sangría debajo de él."),
                Target: "AgentsList"),
            new TourStep(T("Artefatos", "Artifacts", "Artefactos"),
                T("Tudo o que os agentes produzem fica guardado aqui: a spec, o plano em fases, as revisões e o registro de decisões.",
                  "Everything the agents produce is kept here: the spec, the phased plan, the reviews and the decision log.",
                  "Todo lo que producen los agentes queda guardado aquí: la spec, el plan por fases, las revisiones y el registro de decisiones."),
                Target: "ArtifactsList"),
            new TourStep(T("Abas de trabalho", "Work tabs", "Pestañas de trabajo"),
                T("Plano, Fases, Diff, Terminal, Log, Laboratório IA e Canais. As próximas lições passam por elas.",
                  "Plan, Phases, Diff, Terminal, Log, AI Lab and Channels. The next lessons walk through them.",
                  "Plan, Fases, Diff, Terminal, Log, Laboratorio IA y Canales. Las próximas lecciones las recorren."),
                Target: "MainTabs"),
            new TourStep(T("Decisões", "Decisions", "Decisiones"),
                T("O painel mais importante: ações que um agente quer executar e que precisam da sua aprovação. " +
                  "Cada cartão mostra o risco e por que ele foi classificado assim.",
                  "The most important panel: actions an agent wants to run that need your approval. " +
                  "Each card shows the risk and why it was classified that way.",
                  "El panel más importante: acciones que un agente quiere ejecutar y que necesitan su aprobación. " +
                  "Cada tarjeta muestra el riesgo y por qué se clasificó así."),
                Target: "DecisionsPanel"),
            new TourStep(T("Verificação", "Verification", "Verificación"),
                T("Ao fim de cada fase, outro agente revisa o código contra o plano. Os achados vêm por gravidade: crítico, maior e menor.",
                  "At the end of each phase, another agent reviews the code against the plan. Findings come by severity: critical, major and minor.",
                  "Al final de cada fase, otro agente revisa el código contra el plan. Los hallazgos vienen por gravedad: crítico, mayor y menor."),
                Target: "VerificationPanel"),
            new TourStep(T("Barra de status", "Status bar", "Barra de estado"),
                T("Worktrees ativos, consumo de tokens, tamanho do log e o work item vinculado — o resumo da sessão num relance.",
                  "Active worktrees, token usage, log size and the linked work item — the session summary at a glance.",
                  "Worktrees activos, consumo de tokens, tamaño del log y el work item vinculado — el resumen de la sesión de un vistazo."),
                Target: "StatusBar"),
        }),

        new Lesson("fases", T("Plano e fases", "Plan and phases", "Plan y fases"),
            T("Como uma tarefa vira fases, arquivos e diff.", "How a task becomes phases, files and a diff.",
              "Cómo una tarea se convierte en fases, archivos y diff."), 3, new[]
        {
            new TourStep(T("O plano", "The plan", "El plan"),
                T("Toda tarefa começa por uma intenção e critérios de aceite. O fluxo é sempre: Spec → Fases → Execução → Verificação.",
                  "Every task starts with an intent and acceptance criteria. The flow is always: Spec → Phases → Execution → Verification.",
                  "Toda tarea comienza con una intención y criterios de aceptación. El flujo es siempre: Spec → Fases → Ejecución → Verificación."),
                Target: "PlanContent", Tab: TabPlano),
            new TourStep(T("As fases", "The phases", "Las fases"),
                T("O plano é quebrado em fases pequenas, cada uma com um agente responsável e um worktree próprio — dois agentes nunca mexem na mesma cópia do código. " +
                  "Clique numa fase para ver o plano dela.",
                  "The plan is split into small phases, each with an owner agent and its own worktree — two agents never touch the same copy of the code. " +
                  "Click a phase to see its plan.",
                  "El plan se divide en fases pequeñas, cada una con un agente responsable y su propio worktree — dos agentes nunca modifican la misma copia del código. " +
                  "Haga clic en una fase para ver su plan."),
                Target: "PhaseList", Tab: TabFases),
            new TourStep(T("Plano por arquivo", "Per-file plan", "Plan por archivo"),
                T("Antes de escrever código, o agente diz quais arquivos vai criar ou alterar e por quê. É isso que a verificação usa depois para conferir.",
                  "Before writing code, the agent states which files it will create or change and why. That is what the verification checks against later.",
                  "Antes de escribir código, el agente indica qué archivos va a crear o modificar y por qué. Es lo que la verificación usa después para comprobar."),
                Target: "PhaseFiles", Tab: TabFases),
            new TourStep("Diff",
                T("O que o agente realmente mudou, linha a linha: verde é adicionado, vermelho é removido.",
                  "What the agent actually changed, line by line: green is added, red is removed.",
                  "Lo que el agente realmente cambió, línea por línea: verde es agregado, rojo es eliminado."),
                Target: "DiffView", Tab: TabDiff),
            new TourStep("Terminal",
                T("A saída ao vivo do agente. Repare nas linhas [NXPilot]: é o gate interceptando cada ferramenta antes de ela rodar.",
                  "The agent's live output. Note the [NXPilot] lines: that is the gate intercepting each tool before it runs.",
                  "La salida en vivo del agente. Observe las líneas [NXPilot]: es el gate interceptando cada herramienta antes de que se ejecute."),
                Target: "TerminalView", Tab: TabTerminal),
        }),

        new Lesson("decisoes", T("Decidindo ações dos agentes", "Deciding agent actions", "Decidiendo acciones de los agentes"),
            T("Aprovar, negar e o que é aprovado sozinho.", "Approve, deny and what gets approved automatically.",
              "Aprobar, denegar y lo que se aprueba automáticamente."), 3, new[]
        {
            new TourStep(T("Um pedido de decisão", "A decision request", "Una solicitud de decisión"),
                T("Cada cartão traz: o agente, a ação exata (comando ou arquivo), o contexto da fase e o risco — com a confiança e o motivo.",
                  "Each card shows: the agent, the exact action (command or file), the phase context and the risk — with confidence and reason.",
                  "Cada tarjeta muestra: el agente, la acción exacta (comando o archivo), el contexto de la fase y el riesgo — con la confianza y el motivo."),
                Target: "DecisionsPanel"),
            new TourStep(T("Faça você: um agente pede algo", "Your turn: an agent asks for something", "Hágalo usted: un agente pide algo"),
                T("Clique em \"Faça agora\" (ou no botão Simular da barra) e veja um novo pedido chegar ao painel de decisões já classificado.",
                  "Click \"Do it now\" (or the Simulate button on the bar) and watch a new request arrive in the decisions panel, already classified.",
                  "Haga clic en \"Hágalo ahora\" (o en el botón Simular de la barra) y vea llegar una nueva solicitud al panel de decisiones, ya clasificada."),
                Target: "SimulateButton", TryLabel: T("Faça agora: simular pedido", "Do it now: simulate a request", "Hágalo ahora: simular solicitud"),
                Try: vm => vm.SimulateAgentActionCommand.ExecuteAsync(null)),
            new TourStep(T("Aprovar ou negar", "Approve or deny", "Aprobar o denegar"),
                T("Risco MÉDIO ou ALTO sempre espera por você. Aprove para o agente continuar ou negue para bloquear — " +
                  "a área destacada continua clicável durante o tour.",
                  "MEDIUM or HIGH risk always waits for you. Approve to let the agent continue or deny to block it — " +
                  "the highlighted area stays clickable during the tour.",
                  "El riesgo MEDIO o ALTO siempre lo espera a usted. Apruebe para que el agente continúe o deniegue para bloquearlo — " +
                  "el área resaltada sigue siendo clicable durante el recorrido."),
                Target: "DecisionsPanel"),
            new TourStep(T("Aprovado sozinho", "Approved automatically", "Aprobado automáticamente"),
                T("Só o que é BAIXO com confiança (ler arquivo, git status, rodar testes) passa sem perguntar — e mesmo assim fica no log.",
                  "Only confident LOW (reading a file, git status, running tests) goes through without asking — and it is still logged.",
                  "Solo lo que es BAJO con confianza (leer un archivo, git status, ejecutar pruebas) pasa sin preguntar — y aun así queda en el log."),
                Target: "DecisionsPanel"),
            new TourStep(T("Log de eventos", "Event log", "Log de eventos"),
                T("Toda ação, classificação e decisão — sua ou automática — fica registrada aqui: quem, o quê, quando e por quê. É a trilha de auditoria.",
                  "Every action, classification and decision — yours or automatic — is recorded here: who, what, when and why. It is the audit trail.",
                  "Toda acción, clasificación y decisión — suya o automática — queda registrada aquí: quién, qué, cuándo y por qué. Es la pista de auditoría."),
                Target: "LogView", Tab: TabLog),
        }),

        new Lesson("tarefa", T("Sua primeira tarefa real", "Your first real task", "Su primera tarea real"),
            T("Do pedido ao diff: plano, fases, gate, verificação e Histórico.", "From request to diff: plan, phases, gate, verification and History.",
              "De la solicitud al diff: plan, fases, gate, verificación e Historial."), 4, new[]
        {
            new TourStep(T("Comece por aqui", "Start here", "Comience aquí"),
                T("Nova tarefa abre o assistente: escolha um repositório git, descreva a intenção e os critérios de aceite, " +
                  "o modelo do Claude e quem revisa (Codex, Claude ou ninguém). Use um repositório de teste na primeira vez.",
                  "New task opens the wizard: pick a git repository, describe the intent and the acceptance criteria, " +
                  "the Claude model and who reviews (Codex, Claude or nobody). Use a test repository the first time.",
                  "Nueva tarea abre el asistente: elija un repositorio git, describa la intención y los criterios de aceptación, " +
                  "el modelo de Claude y quién revisa (Codex, Claude o nadie). Use un repositorio de prueba la primera vez."),
                Target: "NewTaskButton"),
            new TourStep(T("Nada muda na sua pasta", "Nothing changes in your folder", "Nada cambia en su carpeta"),
                T("O NXPilot cria um git worktree só para a tarefa, num branch nxpilot/<data>. O agente trabalha lá; " +
                  "a sua pasta de trabalho não é tocada. Juntar o resultado (merge/PR) continua sendo decisão sua.",
                  "NXPilot creates a git worktree just for the task, on an nxpilot/<date> branch. The agent works there; " +
                  "your working folder is not touched. Merging the result (merge/PR) remains your decision.",
                  "NXPilot crea un git worktree solo para la tarea, en una rama nxpilot/<fecha>. El agente trabaja allí; " +
                  "su carpeta de trabajo no se toca. Integrar el resultado (merge/PR) sigue siendo decisión suya.")),
            new TourStep(T("O plano vem antes", "The plan comes first", "El plan va primero"),
                T("O Claude primeiro só LÊ o repositório e propõe um plano em fases, com os arquivos de cada fase. " +
                  "O plano aparece aqui, na aba Plano, com os botões Aprovar e Descartar. Sem aprovação, nada é escrito.",
                  "Claude first only READS the repository and proposes a phased plan, with each phase's files. " +
                  "The plan shows up here, in the Plan tab, with Approve and Discard buttons. Without approval, nothing is written.",
                  "Claude primero solo LEE el repositorio y propone un plan por fases, con los archivos de cada fase. " +
                  "El plan aparece aquí, en la pestaña Plan, con los botones Aprobar y Descartar. Sin aprobación, no se escribe nada."),
                Target: "PlanContent", Tab: TabPlano),
            new TourStep(T("Uma fase de cada vez", "One phase at a time", "Una fase a la vez"),
                T("Aprovado o plano, o agente executa fase por fase. Ao fim de cada fase o NXPilot faz um commit no worktree — " +
                  "um ponto de restauração por fase. Aqui você acompanha o status e o plano por arquivo de cada uma.",
                  "Once the plan is approved, the agent runs phase by phase. At the end of each phase NXPilot commits in the worktree — " +
                  "a restore point per phase. Here you follow each phase's status and per-file plan.",
                  "Una vez aprobado el plan, el agente ejecuta fase por fase. Al final de cada fase NXPilot hace un commit en el worktree — " +
                  "un punto de restauración por fase. Aquí usted sigue el estado y el plan por archivo de cada una."),
                Target: "PhaseList", Tab: TabFases),
            new TourStep(T("O gate continua valendo", "The gate still applies", "El gate sigue vigente"),
                T("Toda ferramenta que o agente quiser usar passa pelo gate ANTES de rodar. Leituras seguem sozinhas; " +
                  "escrever, instalar ou rodar scripts aparece aqui e o agente fica parado até você decidir. " +
                  "Escrever fora do worktree é sempre ALTO.",
                  "Every tool the agent wants to use goes through the gate BEFORE it runs. Reads go through on their own; " +
                  "writing, installing or running scripts shows up here and the agent waits until you decide. " +
                  "Writing outside the worktree is always HIGH.",
                  "Toda herramienta que el agente quiera usar pasa por el gate ANTES de ejecutarse. Las lecturas pasan solas; " +
                  "escribir, instalar o ejecutar scripts aparece aquí y el agente queda detenido hasta que usted decida. " +
                  "Escribir fuera del worktree es siempre ALTO."),
                Target: "DecisionsPanel"),
            new TourStep(T("Verificação cruzada", "Cross-verification", "Verificación cruzada"),
                T("No fim, outro agente — o Codex, em modo somente leitura — revisa o diff contra o plano e os critérios. " +
                  "Os achados aparecem aqui por gravidade; com algum crítico, a tarefa avisa para revisar antes de usar.",
                  "At the end, another agent — Codex, in read-only mode — reviews the diff against the plan and the criteria. " +
                  "Findings show up here by severity; with any critical one, the task warns you to review before using it.",
                  "Al final, otro agente — Codex, en modo de solo lectura — revisa el diff contra el plan y los criterios. " +
                  "Los hallazgos aparecen aquí por gravedad; si hay alguno crítico, la tarea le avisa que revise antes de usarla."),
                Target: "VerificationPanel"),
            new TourStep(T("O resultado", "The result", "El resultado"),
                T("O diff da tarefa inteira, desde o ponto de partida. É o que você leva para o seu repositório quando estiver satisfeito.",
                  "The diff of the whole task, from the starting point. It is what you bring into your repository when you are satisfied.",
                  "El diff de toda la tarea, desde el punto de partida. Es lo que usted lleva a su repositorio cuando esté satisfecho."),
                Target: "DiffView", Tab: TabDiff),
            new TourStep(T("Histórico", "History", "Historial"),
                T("Cada tarefa fica guardada com plano, fases, verificação, custo e o log de auditoria. " +
                  "Pelo Histórico você reabre uma tarefa na tela ou remove o worktree dela quando não precisar mais.",
                  "Each task is kept with its plan, phases, verification, cost and audit log. " +
                  "From History you reopen a task on screen or remove its worktree when you no longer need it.",
                  "Cada tarea queda guardada con su plan, fases, verificación, costo y log de auditoría. " +
                  "Desde el Historial usted reabre una tarea en pantalla o elimina su worktree cuando ya no lo necesite."),
                Target: "HistoryButton"),
        }),

        new Lesson("cascata", T("A cascata: como o gate decide", "The cascade: how the gate decides", "La cascada: cómo decide el gate"),
            T("Regras → ONNX → LLM, do barato ao caro.", "Rules → ONNX → LLM, from cheap to expensive.",
              "Reglas → ONNX → LLM, de lo barato a lo caro."), 3, new[]
        {
            new TourStep(T("Três degraus", "Three steps", "Tres escalones"),
                T("O NXPilot decide o risco em cascata:\n\n" +
                  "1. Regras — instantâneas, para os casos óbvios (rm -rf, git push --force).\n" +
                  "2. ONNX — um modelo pequeno que roda em milissegundos na CPU, para o que as regras não conhecem.\n" +
                  "3. LLM (LLaMA local ou Copilot/Azure) — só quando o ONNX fica em dúvida. É mais lento, mas acerta mais.\n\n" +
                  "Assim você paga o modelo caro apenas nos casos difíceis.",
                  "NXPilot decides risk as a cascade:\n\n" +
                  "1. Rules — instant, for the obvious cases (rm -rf, git push --force).\n" +
                  "2. ONNX — a small model that runs in milliseconds on the CPU, for what the rules don't know.\n" +
                  "3. LLM (local LLaMA or Copilot/Azure) — only when ONNX is unsure. Slower, but more accurate.\n\n" +
                  "This way you only pay for the expensive model in the hard cases.",
                  "NXPilot decide el riesgo en cascada:\n\n" +
                  "1. Reglas — instantáneas, para los casos obvios (rm -rf, git push --force).\n" +
                  "2. ONNX — un modelo pequeño que se ejecuta en milisegundos en la CPU, para lo que las reglas no conocen.\n" +
                  "3. LLM (LLaMA local o Copilot/Azure) — solo cuando ONNX tiene dudas. Es más lento, pero acierta más.\n\n" +
                  "Así usted paga el modelo caro solo en los casos difíciles.")),
            new TourStep(T("Qual classificador está ativo", "Which classifier is active", "Qué clasificador está activo"),
                T("A barra superior sempre mostra o gate em uso. Com um LLM disponível, a cascata completa é o padrão.",
                  "The top bar always shows the gate in use. With an LLM available, the full cascade is the default.",
                  "La barra superior siempre muestra el gate en uso. Con un LLM disponible, la cascada completa es la opción predeterminada."),
                Target: "GateStatusText"),
            new TourStep(T("ONNX: CPU ou GPU", "ONNX: CPU or GPU", "ONNX: CPU o GPU"),
                T("CPU é o padrão e funciona em qualquer computador. A GPU é opcional — e, para este modelo pequeno, a CPU costuma ser até mais rápida.",
                  "CPU is the default and works on any computer. The GPU is optional — and for this small model the CPU is often even faster.",
                  "La CPU es la opción predeterminada y funciona en cualquier computadora. La GPU es opcional — y, para este modelo pequeño, la CPU suele ser incluso más rápida."),
                Target: "LabOnnxCard", Tab: TabLab),
            new TourStep(T("O LLM da dúvida", "The LLM for doubtful cases", "El LLM para los casos dudosos"),
                T("O 3º degrau pode ser a IA Local (LLaMA, nada sai da máquina) ou um provedor da empresa: " +
                  "GitHub Copilot, Azure OpenAI, Microsoft 365 ou OpenRouter — em \"Trocar provedor…\". A caixa liga ou desliga o 3º degrau.",
                  "Step 3 can be the Local AI (LLaMA, nothing leaves the machine) or a company provider: " +
                  "GitHub Copilot, Azure OpenAI, Microsoft 365 or OpenRouter — in \"Change provider…\". The checkbox turns step 3 on or off.",
                  "El 3.er escalón puede ser la IA Local (LLaMA, nada sale de la máquina) o un proveedor de la empresa: " +
                  "GitHub Copilot, Azure OpenAI, Microsoft 365 u OpenRouter — en \"Cambiar proveedor…\". La casilla activa o desactiva el 3.er escalón."),
                Target: "LabLlamaCard", Tab: TabLab),
            new TourStep(T("Faça você: comparar os motores", "Your turn: compare the engines", "Hágalo usted: comparar los motores"),
                T("A comparação roda as mesmas ações em cada motor e mostra velocidade e acertos lado a lado. " +
                  "\"Faça agora\" roda a versão rápida (sem o LLaMA), em poucos segundos.",
                  "The comparison runs the same actions on each engine and shows speed and accuracy side by side. " +
                  "\"Do it now\" runs the quick version (without LLaMA) in a few seconds.",
                  "La comparación ejecuta las mismas acciones en cada motor y muestra velocidad y aciertos lado a lado. " +
                  "\"Hágalo ahora\" ejecuta la versión rápida (sin LLaMA), en pocos segundos."),
                Target: "LabCompare", Tab: TabLab, TryLabel: T("Faça agora: comparar", "Do it now: compare", "Hágalo ahora: comparar"),
                Try: vm => { vm.Lab.BenchLlama = false; return vm.Lab.RunBenchmarkCommand.ExecuteAsync(null); }),
        }),

        new Lesson("ensinar", T("Ensinando o classificador", "Teaching the classifier", "Enseñando al clasificador"),
            T("Corrigir um erro e ver o gate aprender na hora.", "Fix a mistake and watch the gate learn right away.",
              "Corregir un error y ver al gate aprender al instante."), 2, new[]
        {
            new TourStep(T("Testar uma ação", "Test an action", "Probar una acción"),
                T("Escreva qualquer comando e clique em Classificar: cada motor mostra o risco, a confiança e o tempo.",
                  "Type any command and click Classify: each engine shows the risk, the confidence and the time.",
                  "Escriba cualquier comando y haga clic en Clasificar: cada motor muestra el riesgo, la confianza y el tiempo."),
                Target: "LabTry", Tab: TabLab, TryLabel: T("Faça agora: classificar o exemplo", "Do it now: classify the example", "Hágalo ahora: clasificar el ejemplo"),
                Try: vm => vm.Lab.TryClassifyCommand.ExecuteAsync(null)),
            new TourStep(T("Ensinar", "Teach", "Enseñar"),
                T("Se o ONNX errou, diga o risco certo. A ação vira um exemplo na hora — sem re-treinar — e ações parecidas passam a ser classificadas do mesmo jeito.",
                  "If ONNX got it wrong, tell it the right risk. The action becomes an example right away — no retraining — and similar actions get classified the same way.",
                  "Si ONNX se equivocó, indique el riesgo correcto. La acción se convierte en un ejemplo al instante — sin reentrenar — y las acciones similares pasan a clasificarse de la misma manera."),
                Target: "LabTeach", Tab: TabLab),
            new TourStep(T("O gate aprende com a equipe", "The gate learns from the team", "El gate aprende del equipo"),
                T("Na versão completa, cada decisão que você corrige no painel vira exemplo automaticamente. " +
                  "Com o tempo, o gate aprende a política de risco da sua equipe.\n\nVocê concluiu as lições básicas!",
                  "In the full version, every decision you correct in the panel automatically becomes an example. " +
                  "Over time, the gate learns your team's risk policy.\n\nYou have completed the basic lessons!",
                  "En la versión completa, cada decisión que usted corrige en el panel se convierte automáticamente en un ejemplo. " +
                  "Con el tiempo, el gate aprende la política de riesgo de su equipo.\n\n¡Usted completó las lecciones básicas!")),
        }),
    };
}
