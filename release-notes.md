## NXPilot Community {versao}

> Inteligência Artificial Automática, e não autônoma — porque você está no controle.

### Como instalar e atualizar

O NXPilot agora é publicado em duas partes separadas, como o NXProject:

- **NXPilot-Setup.zip** (release fixa [Setup (instalador)](https://github.com/nexusxdata/NXPilot/releases/tag/nxpilot-setup-latest)): o instalador e a **base** (runtime .NET e bibliotecas de terceiros), **sem** os arquivos do app. Só é republicado quando a base ou o instalador mudam.
- **NXPilot-Release.zip** (esta página): o **app** (`NxPilot.exe` e as DLLs do NXPilot), com menos de 1 MB.

- **Primeira instalação:** baixe o NXPilot-Setup.zip, extraia e rode `NXPilot-Setup.exe`. Ele baixa o NXPilot-Release.zip mais novo do GitHub (com SHA-256 conferido), instala e cria o atalho. Precisa de internet.
- **Já tem o NXPilot:** abra **⚙ Configurar** e use **Verificar atualização**. O app baixa só o NXPilot-Release.zip, troca os arquivos e reabre. Nenhum script é executado.
- **Voltar versão:** em **⚙ Configurar**, escolha uma versão anterior e clique em **Instalar esta versão**.

> **Esta versão traz uma biblioteca nova na base** (o git embutido). Quem atualizar por **Verificar atualização** recebe o aviso e o app baixa o Setup novo sozinho.

### Novidades

**🗂 Criar tarefa sem git: versionamento local por assunto**

- A Nova tarefa tem o campo **Versionamento**. **Local** (o padrão) não exige git nem Git instalado. **Repositório git do projeto** é para clones do GitHub, Azure DevOps e outros.
- No modo local, cada **assunto** é uma pasta comum (por exemplo, `Documentos\NXPilot\Relatórios`) onde ficam os arquivos. O histórico fica numa pasta oculta ao lado, e a pasta do assunto não ganha `.git`.
- A tarefa trabalha numa cópia isolada. A pasta do assunto só muda quando você clica em **Aplicar na pasta original**. Se você também mexeu num arquivo depois que a tarefa começou, o NXPilot mostra o conflito e só sobrescreve se você confirmar.
- Na janela **Assuntos…**:
  - **Pasta dos assuntos:** pode ser uma pasta do OneDrive ou do Google Drive, para ter backup.
  - **Mover para outra pasta…:** leva a pasta e o histórico juntos.
  - **Mover para o GitHub…:** cria um repositório privado e envia o histórico. Pede a sua aprovação, porque é push na branch principal (risco ALTO).
- O git agora vem embutido no NXPilot (LibGit2Sharp, licença MIT), inclusive no modo git, que deixou de precisar do Git instalado.

**✅ Decisões mais rápidas, sem perder o controle**

- **Aprovar semelhantes:** aprova o pedido e libera, até o fim da tarefa, os próximos com o mesmo comando (com outros argumentos).
- **Aprovar médio e baixo:** deixa os pedidos de risco médio e baixo passarem sozinhos até o fim da tarefa.
- **Risco ALTO sempre volta para você**, inclusive gravar fora do worktree. As regras valem só para a tarefa atual, aparecem no painel com o botão **Desligar** e cada aprovação automática fica registrada no Log.
- **Ver script:** quando o pedido roda um `.py`, `.ps1`, `.js` e similares (inclusive `python -m pacote.modulo`), o arquivo abre numa janela de leitura do NXPilot, nunca pelo programa padrão, que executaria o script.

**🔁 Tarefas**

- **Retomar de onde parou:** uma tarefa interrompida ou que falhou continua a partir da fase que não terminou.
- **Analisar e corrigir achados:** quando a verificação cruzada não aprova, os achados voltam para o agente. Ele corrige o que procede (sob o gate), e a verificação roda de novo.
- **Arquivos do resultado:** o card da tarefa lista o que foi criado ou alterado, com **Abrir** (só para documentos) e **Mostrar na pasta**.
- **Indicador de execução:** barra animada e cronômetro enquanto o agente trabalha.
- **Nova tarefa** em duas colunas: mais larga e mais baixa.
