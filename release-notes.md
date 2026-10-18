## NXPilot Community {versao}

> Inteligência Artificial Automática, e não autônoma — porque você está no controle.

### Como instalar e atualizar (novo)

- **Primeira instalação:** baixe o **NXPilot-Setup.zip** na release [Setup (instalador)](https://github.com/nexusxdata/NXPilot/releases/tag/nxpilot-setup-latest), extraia e rode `NXPilot-Setup.exe`. O Setup instala a base e baixa a versão mais nova do app. Sem internet, ele instala a versão que vem dentro dele.
- **Já tem o NXPilot:** abra **⚙ Configurar** e use **Verificar atualização**. Ele baixa só o `NXPilot-Release.zip` desta página, que tem poucos MB, confere o SHA-256 e reinicia o app. Não roda nenhum script, então funciona mesmo com antivírus que bloqueia PowerShell.
- **Voltar versão:** em **⚙ Configurar**, escolha uma versão anterior e clique em **Instalar esta versão**. Suas configurações, tarefas e histórico continuam lá.

### Novidades

**🧩 Extensões (prévia)**
- **Power BI Desktop.** O agente edita o projeto `.pbip` como código: o modelo em TMDL e o relatório em PBIR. O NXPilot valida os arquivos e, para conferir, abre o projeto no Power BI e captura a tela. Ele não clica em nada.
- **Janelas do Windows (prévia).** Lista janelas, lê os textos e captura a tela, **só dos apps que você liberar** (por exemplo, Excel ou SAP GUI). Clicar e digitar virão na próxima versão, sempre com a sua aprovação.
- **Databricks SQL**, **Git (GitHub e Azure DevOps)** e **E-mail (Outlook e Gmail, somente leitura)**, como na versão anterior.

**Instalação do Claude Code e do Codex** (passo 3 do Setup)
- **Automática (recomendada):** fica na área do usuário, sem administrador e sem script. Baixa do site oficial, confere o SHA-256 e usa as mesmas pastas do instalador oficial.
- **Script oficial:** roda o instalador da Anthropic ou da OpenAI numa janela do PowerShell. O botão **Copiar** pega o comando para você instalar manualmente.
- **Versão:** escolha entre a **última** e a **testada com o NXPilot**. Se a testada sair do ar, o Setup instala a última e avisa.

> **Prévia:** as extensões foram testadas com servidores simulados e ainda não com contas reais. Se encontrar um problema, abra uma *issue*.
