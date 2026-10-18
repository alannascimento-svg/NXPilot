# NXPilot.WindowsUi

Acesso às janelas do Windows pela UI Automation ([FlaUI](https://github.com/FlaUI/FlaUI), licença MIT).
É a base comum de duas extensões do NXPilot: **Power BI Desktop** e **Automatizar janelas do Windows**.

## Nesta versão: só ver

| Operação | O que faz |
|---|---|
| `ListWindows` | Lista as janelas abertas com título, processo e posição. |
| `FindWindow` / `LaunchAndWaitAsync` | Acha a janela pelo **processo** (o título pode ser só o nome do arquivo) ou abre o programa e espera a janela aparecer. |
| `Capture` | Gera um PNG da janela. O processo precisa ser DPI aware, o que o WPF já é; senão, com escala de 125%, a imagem sai cortada. |
| `ReadElements` / `ToAgentContext` | Lê a árvore de elementos (nome, tipo, valor) e entrega ao agente como **dado não confiável**. |

**Clicar, digitar e fechar não existem nesta versão.** Quando vierem, cada ação vai passar pelo DecisionGate com o print do ponto e com a sua aprovação.

## Política

Todas as operações conferem a `WindowsUiPolicy`, uma lista de **processos liberados** (por exemplo, `PBIDesktop` ou `EXCEL`).
Fora da lista, nada é lido nem capturado, e a operação lança `WindowsUiDeniedException`.
Se a janela mudar de dono, a política é conferida de novo ao reabrir a janela pelo handle.

```csharp
using var ui = new WindowsUiService(WindowsUiPolicy.Parse("PBIDesktop\nEXCEL"));
var excel = ui.FindWindow("EXCEL") ?? throw new Exception("Excel fechado");
ui.Capture(excel, @"C:\temp\excel.png");
var paraOAgente = ui.ToAgentContext(excel, ui.ReadElements(excel));
```
