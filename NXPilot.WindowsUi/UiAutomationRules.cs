// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NXPilot.WindowsUi;

// Regras SEM dependência (o teste crítico compila este arquivo direto): limites do monitor,
// processo de passos aprovado por hash e o executor que aborta em qualquer dúvida.

/// <summary>Monitorar um app: intervalo mínimo de 20 s (sem loop) e tempo limite obrigatório.</summary>
public sealed record MonitorOptions
{
    public static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    public TimeSpan Interval { get; }
    public TimeSpan Duration { get; }

    /// <summary>Lança se o intervalo for menor que 20 s ou a duração passar de 8 h — a regra vale no código, não só na tela.</summary>
    public MonitorOptions(TimeSpan interval, TimeSpan duration)
    {
        if (interval < MinInterval) throw new ArgumentOutOfRangeException(nameof(interval), $"O intervalo mínimo é {MinInterval.TotalSeconds:F0} s.");
        if (duration <= TimeSpan.Zero || duration > MaxDuration) throw new ArgumentOutOfRangeException(nameof(duration), $"O tempo limite vai de 1 min a {MaxDuration.TotalHours:F0} h.");
        if (duration < interval) throw new ArgumentOutOfRangeException(nameof(duration), "O tempo limite precisa ser maior que o intervalo.");
        Interval = interval;
        Duration = duration;
    }

    /// <summary>Quantas leituras cabem no tempo limite (a primeira é imediata).</summary>
    public int MaxReads => (int)(Duration.Ticks / Interval.Ticks) + 1;

    /// <summary>Palavras que disparam o aviso (vazio = avisa a cada mudança).</summary>
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Origem: o texto que identifica a área a observar (ex.: o nome no cabeçalho da conversa
    /// aberta). Só conta o que vem DEPOIS da última linha que é exatamente esse texto; sem a
    /// origem visível, nada dispara. Vazio = a janela toda.
    /// </summary>
    public string? Origin { get; init; }

    /// <summary>"urgente, pedido; nxpilot" → ["urgente", "pedido", "nxpilot"].</summary>
    public static IReadOnlyList<string> ParseKeywords(string text) =>
        text.Split([',', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}

/// <summary>O que dispara o aviso do monitor: só LINHAS NOVAS (que não estavam na leitura anterior).</summary>
public static class MonitorMatcher
{
    private static readonly System.Globalization.CompareInfo Compare = System.Globalization.CultureInfo.InvariantCulture.CompareInfo;
    private const System.Globalization.CompareOptions Loose =
        System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace; // sem maiúsculas nem acentos

    public static bool Contains(string text, string keyword) => Compare.IndexOf(text, keyword, Loose) >= 0;

    /// <summary>
    /// Recorta a área da origem: as linhas DEPOIS da última linha que é exatamente
    /// <paramref name="origin"/> (sem maiúsculas/acentos). Ex.: no navegador, a lista lateral vem
    /// antes do cabeçalho da conversa aberta e as mensagens dela, depois. Sem origem: o texto todo.
    /// Origem não visível: null (não dispara).
    /// </summary>
    public static string? Scope(string text, string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin)) return text;
        var lines = text.Split('\n');
        for (var i = lines.Length - 1; i >= 0; i--)
            if (Compare.Compare(lines[i].Trim(), origin.Trim(), Loose) == 0)
                return string.Join('\n', lines.Skip(i + 1));
        return null;
    }

    /// <summary>
    /// Linhas que surgiram desde a leitura anterior e contêm alguma palavra-chave. Sem palavras:
    /// todas as linhas novas. Na primeira leitura (sem anterior) não dispara — é o "estado inicial".
    /// </summary>
    public static IReadOnlyList<string> NewMatches(string? previous, string current, IReadOnlyList<string> keywords)
    {
        if (previous == null) return Array.Empty<string>();
        var before = new HashSet<string>(Lines(previous));
        return Lines(current)
            .Where(l => !before.Contains(l) && (keywords.Count == 0 || keywords.Any(k => Contains(l, k))))
            .Distinct()
            .ToList();
    }

    private static IEnumerable<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum UiStepKind { Click, Type, Key, Wait }

/// <summary>
/// Alvo de um passo, escolhido na lista de elementos do app: tipo + AutomationId e/ou nome.
/// Na execução precisa casar com EXATAMENTE um elemento — senão o processo para.
/// </summary>
public sealed record UiTarget(string ControlType, string? AutomationId, string? Name)
{
    public override string ToString() => $"[{ControlType}] {(string.IsNullOrEmpty(Name) ? AutomationId : Name)}";
}

public sealed record UiStep(UiStepKind Kind, UiTarget? Target = null, string? Text = null, string? Key = null, int WaitSeconds = 0)
{
    public string Describe() => Kind switch
    {
        UiStepKind.Click => $"Clicar em {Target}",
        UiStepKind.Type => $"Digitar \"{Text}\" em {Target}",
        UiStepKind.Key => $"Apertar {Key}",
        UiStepKind.Wait => $"Esperar {WaitSeconds} s",
        _ => Kind.ToString(),
    };

    /// <summary>Teclas aceitas (sem atalhos de sistema como Win+R ou Alt+F4, e sem Delete).</summary>
    public static readonly string[] AllowedKeys = { "Enter", "Tab", "Escape", "Up", "Down", "Left", "Right", "PageUp", "PageDown", "Home", "End", "F5" };

    /// <summary>
    /// Palavras de EXCLUSÃO (PT/EN/ES). Clique num elemento assim pede nova autorização na hora,
    /// mesmo com o processo aprovado. "Enviar" não entra: vale a aprovação do processo.
    /// </summary>
    public static readonly string[] DestructiveWords =
    {
        "excluir", "exclusão", "apagar", "deletar", "remover", "eliminar", "descartar", "esvaziar", "lixeira", "limpar tudo",
        "delete", "remove", "erase", "discard", "purge", "trash", "drop", "wipe", "borrar",
    };

    /// <summary>O texto (nome ou AutomationId) indica exclusão?</summary>
    public static bool LooksDestructive(string? text) =>
        !string.IsNullOrWhiteSpace(text) && DestructiveWords.Any(w => text.Contains(w, StringComparison.OrdinalIgnoreCase));

    /// <summary>Clique cujo alvo, como gravado no processo, indica exclusão.</summary>
    public bool IsDestructive => Kind == UiStepKind.Click && Target is { } t && (LooksDestructive(t.Name) || LooksDestructive(t.AutomationId));
}

/// <summary>
/// Processo: sequência de passos num app. A aprovação guarda o hash do conteúdo — qualquer
/// edição (passo, alvo, texto, app) muda o hash e a aprovação deixa de valer.
/// </summary>
public sealed record UiProcess(string Name, string ProcessName, IReadOnlyList<UiStep> Steps)
{
    /// <summary>Quem escreveu a sequência: "usuário" ou "agente" (o agente pode propor; só você aprova).</summary>
    public string Author { get; init; } = "usuário";
    public string? ApprovedHash { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }

    private static readonly JsonSerializerOptions Canonical = new() { Converters = { new JsonStringEnumConverter() } };

    public string ContentHash() => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
        JsonSerializer.Serialize(new { App = WindowsUiPolicy.Normalize(ProcessName).ToLowerInvariant(), Steps }, Canonical))));

    public bool IsApproved => ApprovedHash != null && ApprovedHash == ContentHash();

    public UiProcess Approve() => this with { ApprovedHash = ContentHash(), ApprovedAt = DateTimeOffset.Now };

    /// <summary>Problemas que impedem aprovar (lista vazia = pode aprovar).</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (Steps.Count == 0) problems.Add("O processo não tem passos.");
        if (Steps.Count > 100) problems.Add("No máximo 100 passos por processo.");
        for (var i = 0; i < Steps.Count; i++)
        {
            var s = Steps[i];
            var n = $"Passo {i + 1}";
            if (s.Kind is UiStepKind.Click or UiStepKind.Type && s.Target is not { } t) problems.Add($"{n}: escolha o elemento.");
            else if (s.Target is { } tg && string.IsNullOrEmpty(tg.AutomationId) && string.IsNullOrEmpty(tg.Name)) problems.Add($"{n}: o elemento não tem nome nem AutomationId — não dá para achá-lo com segurança.");
            if (s.Kind == UiStepKind.Type && string.IsNullOrEmpty(s.Text)) problems.Add($"{n}: informe o texto.");
            if (s.Kind == UiStepKind.Key && !UiStep.AllowedKeys.Contains(s.Key, StringComparer.OrdinalIgnoreCase)) problems.Add($"{n}: tecla não permitida ({s.Key}).");
            if (s.Kind == UiStepKind.Wait && s.WaitSeconds is < 1 or > 300) problems.Add($"{n}: espera entre 1 e 300 s.");
        }
        return problems;
    }
}

/// <summary>O que o executor precisa da tela — o FlaUI implementa; o teste usa um falso.</summary>
public interface IUiDriver
{
    /// <summary>Quantos elementos casam com o alvo na janela do app (0, 1 ou mais).</summary>
    int Count(UiTarget target);
    /// <summary>Nome real do elemento na tela agora (pode diferir do gravado, ex.: alvo achado só pelo AutomationId).</summary>
    string LiveName(UiTarget target);
    void Click(UiTarget target);
    void TypeText(UiTarget target, string text);
    void PressKey(string key);
}

public sealed record UiStepResult(int Index, UiStep Step, bool Ok, string Message);

/// <summary>
/// Executa um processo APROVADO, passo a passo. Para no primeiro problema: alvo inexistente,
/// alvo ambíguo (mais de um), erro do app ou cancelamento. Nunca "chuta" o elemento.
/// Clique de EXCLUSÃO (pelo nome gravado OU pelo nome real na tela) só acontece com nova
/// autorização na hora (<paramref name="confirmDestructive"/>); sem ela, o processo para.
/// </summary>
public static class UiProcessRunner
{
    public static async Task<IReadOnlyList<UiStepResult>> RunAsync(UiProcess process, IUiDriver driver,
        IProgress<UiStepResult>? progress = null, CancellationToken ct = default,
        Func<UiStep, string, CancellationToken, Task<bool>>? confirmDestructive = null)
    {
        if (!process.IsApproved)
            throw new InvalidOperationException("O processo não está aprovado (ou foi alterado depois da aprovação). Revise e aprove de novo.");
        var results = new List<UiStepResult>();
        for (var i = 0; i < process.Steps.Count; i++)
        {
            var step = process.Steps[i];
            UiStepResult r;
            try
            {
                ct.ThrowIfCancellationRequested();
                if (step.Target is { } target)
                {
                    var count = driver.Count(target);
                    if (count != 1)
                    {
                        r = new(i, step, false, count == 0
                            ? $"Elemento não encontrado: {target}. Processo interrompido."
                            : $"{count} elementos iguais a {target} — ambíguo. Processo interrompido.");
                        results.Add(r); progress?.Report(r);
                        break;
                    }
                    var live = driver.LiveName(target);
                    if (step.Kind == UiStepKind.Click && (step.IsDestructive || UiStep.LooksDestructive(live)))
                    {
                        var ok = confirmDestructive != null && await confirmDestructive(step, live, ct);
                        if (!ok)
                        {
                            r = new(i, step, false, $"Exclusão não autorizada (\"{live}\"). Processo interrompido — nada foi excluído.");
                            results.Add(r); progress?.Report(r);
                            break;
                        }
                    }
                }
                switch (step.Kind)
                {
                    case UiStepKind.Click: driver.Click(step.Target!); break;
                    case UiStepKind.Type: driver.TypeText(step.Target!, step.Text!); break;
                    case UiStepKind.Key: driver.PressKey(step.Key!); break;
                    case UiStepKind.Wait: await Task.Delay(TimeSpan.FromSeconds(step.WaitSeconds), ct); break;
                }
                r = new(i, step, true, step.Describe());
            }
            catch (OperationCanceledException) { r = new(i, step, false, "Interrompido por você."); results.Add(r); progress?.Report(r); break; }
            catch (Exception e) { r = new(i, step, false, $"Falhou: {e.Message}. Processo interrompido."); results.Add(r); progress?.Report(r); break; }
            results.Add(r); progress?.Report(r);
        }
        return results;
    }
}
