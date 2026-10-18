// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Messaging;

/// <summary>
/// Fonte de demonstração: passa os payloads de contrato pelo MESMO parser da API oficial,
/// sem conta na Meta. Serve para treinar a triagem e mostrar o fluxo antes de ter o chip.
/// </summary>
public sealed class SimulatedMessageSource : IMessageSource
{
    private int _next;

    public string Name => "Simulador (sem conta na Meta)";
    public bool IsRunning { get; private set; }

    public event Action<IncomingMessage>? MessageReceived;
    public event Action<string>? StatusChanged;

    public Task StartAsync(CancellationToken ct = default)
    {
        IsRunning = true;
        StatusChanged?.Invoke("Simulador ativo — use \"Receber mensagem de teste\".");
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        IsRunning = false;
        StatusChanged?.Invoke("Parado.");
        return Task.CompletedTask;
    }

    /// <summary>Entrega o próximo payload de exemplo (em rodízio).</summary>
    public void EmitNext()
    {
        if (!IsRunning) return;
        var json = WhatsAppPayloads.All[_next++ % WhatsAppPayloads.All.Length];
        foreach (var m in WhatsAppWebhookParser.Parse(json))
            MessageReceived?.Invoke(m with { Timestamp = DateTimeOffset.Now });
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
