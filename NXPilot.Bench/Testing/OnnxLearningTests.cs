// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using NXPilot.Onnx;
using NXPilot.Risk;
using static NXPilot.Testing.NxAssert;

namespace NXPilot.Testing;

/// <summary>Integração real; exige o modelo ONNX local e usa um arquivo de exemplos temporário.</summary>
public static class OnnxLearningTests
{
    public static IReadOnlyList<CriticalTest> All { get; } = new CriticalTest[]
    {
        new("ONNX: exemplo ensinado sobrevive à recarga sem contaminar o benchmark", async () =>
        {
            var folder = Path.Combine(Path.GetTempPath(), "nx-learning-" + Guid.NewGuid().ToString("N"));
            try
            {
                var models = new OnnxModelStore();
                AssertTrue(models.IsInstalled, "Este teste exige o modelo ONNX baixado no Laboratório IA.");
                var examples = new RiskExampleStore(Path.Combine(folder, "examples.json"));
                const string action = "NXPilot validation: operation requiring explicit human approval";
                int before;
                using (var first = new OnnxRiskClassifier(models, OnnxDevice.Cpu, exampleStore: examples))
                {
                    await first.InitializeAsync();
                    before = first.ExampleCount;
                    first.AddExample(new RiskExample(action, RiskLevel.Alto));
                    AssertEqual(before + 1, first.ExampleCount, "Ensinar inclui o exemplo ativo.");
                }
                using (var reloaded = new OnnxRiskClassifier(models, OnnxDevice.Cpu, exampleStore: new RiskExampleStore(examples.FilePath)))
                {
                    await reloaded.InitializeAsync();
                    AssertEqual(before + 1, reloaded.ExampleCount, "Recriar o gate deve recuperar o exemplo salvo.");
                    reloaded.AddExample(new RiskExample(action, RiskLevel.Medio));
                    AssertEqual(before + 1, reloaded.ExampleCount, "Corrigir o rótulo não duplica a ação.");
                    AssertEqual(RiskLevel.Medio, examples.Load().Single().Level, "A correção também deve ser salva.");
                }
                using var benchmark = new OnnxRiskClassifier(models, OnnxDevice.Cpu);
                await benchmark.InitializeAsync();
                AssertEqual(before, benchmark.ExampleCount, "O benchmark continua usando apenas os exemplos de referência.");
            }
            finally { await TestCleanup.DeleteFlatDirectoryAsync(folder); }
        }),
    };
}
