## NXPilot Community {versao}

> Inteligência Artificial Automática, e não autônoma — porque você está no controle.

### Como instalar e atualizar

- **Primeira instalação:** baixe o **NXPilot-Setup.zip** na release [Setup (instalador)](https://github.com/nexusxdata/NXPilot/releases/tag/nxpilot-setup-latest), extraia e rode `NXPilot-Setup.exe`. O Setup instala a base e já baixa a versão mais nova do app.
- **Já tem o NXPilot:** abra **⚙ Configurar** e use **Verificar atualização**. O app baixa só o `NXPilot-Release.zip` desta página, que tem poucos MB, confere o SHA-256 e reinicia. Nenhum script é executado.
- **Voltar versão:** em **⚙ Configurar**, escolha uma versão anterior e clique em **Instalar esta versão**.

### Novidades

**🧩 Janelas do Windows (prévia)**

- **Ler um app.** Escolha qualquer app aberto, mesmo minimizado, e o NXPilot lê o texto que ele expõe pela acessibilidade do Windows. A leitura funciona em apps nativos e em páginas no navegador. O NXPilot só lê: não clica, não fecha e não digita. O texto chega ao agente como dado, nunca como instrução.
- **Monitorar um app**, sempre com sua autorização:
  - **Intervalo:** no mínimo **20 s**.
  - **Tempo limite:** até 8 h; depois disso o monitor para sozinho.
  - **Palavras-chave:** o aviso só dispara quando aparece uma **linha nova** com alguma delas. Maiúsculas e acentos não importam.
  - **Origem (área):** só conta o texto que aparece depois de um título ou cabeçalho escolhido. Se a origem não estiver na tela, nada dispara.
  - **Ao disparar:** pode executar um processo aprovado.
- **Processos (clicar e digitar).**
  - Você ou o agente monta uma sequência de passos com elementos reais do app: clicar, digitar, tecla e esperar.
  - **Você aprova o processo inteiro.** Qualquer edição desfaz a aprovação.
  - Na execução, se um alvo não existir ou houver mais de um igual, o processo **para**.
  - **Passos de exclusão sempre pedem nova autorização na hora**, mesmo com o processo aprovado.

> **Prévia:** a leitura foi testada em apps reais. Os processos de clicar e digitar e o monitor completo ainda não foram testados em apps reais. Se encontrar um problema, abra uma *issue*.
