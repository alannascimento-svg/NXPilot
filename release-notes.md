## NXPilot Community {versao}

> Inteligência Artificial Automática, e não autônoma — porque você está no controle.

### Como instalar e atualizar

O NXPilot agora é publicado em duas partes separadas, como o NXProject:

- **NXPilot-Setup.zip** (release fixa [Setup (instalador)](https://github.com/nexusxdata/NXPilot/releases/tag/nxpilot-setup-latest)): o instalador e a **base** (runtime .NET e bibliotecas de terceiros), **sem** os arquivos do app. Só é republicado quando a base ou o instalador mudam.
- **NXPilot-Release.zip** (esta página): o **app** (`NxPilot.exe` e as DLLs do NXPilot), com menos de 1 MB.

- **Primeira instalação:** baixe o NXPilot-Setup.zip, extraia e rode `NXPilot-Setup.exe`. Ele baixa o NXPilot-Release.zip mais novo do GitHub (com SHA-256 conferido), instala e cria o atalho. Precisa de internet.
- **Já tem o NXPilot:** abra **⚙ Configurar** e use **Verificar atualização**. O app baixa só o NXPilot-Release.zip, troca os arquivos e reabre. Nenhum script é executado.
- **Voltar versão:** em **⚙ Configurar**, escolha uma versão anterior e clique em **Instalar esta versão**.

### Novidades

**📦 Setup no modelo do NXProject**

- O Setup não leva mais o app junto: traz só a base, e o app vem da release de cada versão. Quando muda só o NXPilot, o Setup não precisa ser publicado de novo.
- Novo **passo 1 · .NET 10 Desktop Runtime**, como no NXProject-Setup: confere se o .NET está instalado e oferece **Instalar .NET 10** (instalador oficial da Microsoft, pede administrador) ou **Baixar no navegador**.

**🟠 Ícone e atalho**

- O NXPilot ganhou ícone próprio: o "NX" da família Nexus XData (o mesmo do NXProject), em **laranja**. Aparece no atalho, na barra de tarefas e no Setup.
- O Setup cria o atalho **NXPilot** na área de trabalho pela API do próprio Windows, sem rodar script, então o antivírus não bloqueia. Na atualização, o atalho antigo é substituído.
- Se mesmo assim não der para criar o atalho, o Setup avisa, em vez de dizer que criou.
