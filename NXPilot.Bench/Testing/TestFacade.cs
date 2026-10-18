// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

namespace NXPilot.Testing;

/// <summary>Teste crítico: nome legível + corpo. Mesma forma do NXTestUnit (nome, ação).</summary>
public sealed record CriticalTest(string Name, Func<Task> Run);

/// <summary>Teste que não se aplica nesta máquina (ex.: sem git) — conta como [SKIP], não como falha.</summary>
public sealed class SkipTestException(string reason) : Exception(reason);

/// <summary>
/// FACHADA de testes do NXPilot. Os testes só conhecem isto (Assert* + CriticalTest +
/// suítes por categoria); o executor fica aqui atrás. Quando o framework de testes do NXProject
/// (NXTestUnit) virar um projeto à parte e entrar no NXPilot, só este arquivo muda — os
/// testes continuam iguais. Por isso os nomes seguem o NXTestUnit: AssertEqual, AssertTrue…
/// </summary>
public static class NxAssert
{
    public static void AssertTrue(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    public static void AssertFalse(bool condition, string message) => AssertTrue(!condition, message);

    public static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{message} Esperado: {expected}; Atual: {actual}.");
    }

    public static void AssertContains(string expectedPart, string? actual, string message)
    {
        if (actual == null || !actual.Contains(expectedPart, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{message} Esperava conter \"{expectedPart}\"; Atual: \"{actual}\".");
    }

    public static async Task AssertThrowsAsync<TException>(Func<Task> action, string message) where TException : Exception
    {
        try { await action(); }
        catch (TException) { return; }
        throw new InvalidOperationException($"{message} Esperava {typeof(TException).Name}.");
    }

    public static void Skip(string reason) => throw new SkipTestException(reason);
}

/// <summary>Executor atual (console, [OK]/[FAIL]/[SKIP] como no NXTestUnit). Trocável sem mexer nos testes.</summary>
public static class TestRunner
{
    public static async Task<int> RunAsync(string title, IReadOnlyList<CriticalTest> tests)
    {
        Console.WriteLine(title);
        Console.WriteLine();
        var failures = new List<string>();
        var skipped = 0;
        foreach (var test in tests)
        {
            try
            {
                await test.Run();
                Console.WriteLine($"[OK]   {test.Name}");
            }
            catch (SkipTestException skip)
            {
                skipped++;
                Console.WriteLine($"[SKIP] {test.Name} — {skip.Message}");
            }
            catch (Exception ex)
            {
                failures.Add($"{test.Name}: {ex.Message}");
                Console.WriteLine($"[FAIL] {test.Name}");
                Console.WriteLine($"       {ex.Message}");
            }
        }
        Console.WriteLine();
        var passed = tests.Count - failures.Count - skipped;
        if (failures.Count == 0)
        {
            Console.WriteLine($"Testes críticos concluídos: {passed} passaram{(skipped > 0 ? $", {skipped} pulados" : "")}.");
            return 0;
        }
        Console.WriteLine($"Testes críticos falharam: {failures.Count} de {tests.Count}.");
        foreach (var f in failures) Console.WriteLine(" - " + f);
        return 1;
    }
}
