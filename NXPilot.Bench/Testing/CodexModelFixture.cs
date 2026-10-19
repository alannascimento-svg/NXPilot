// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text.Json.Nodes;

namespace NXPilot.Testing;

/// <summary>Servidor stdio determinístico usado pelo teste público de descoberta de modelos.</summary>
public static class CodexModelFixture
{
    public const string EnvironmentKey = "NXPILOT_TEST_MODEL_SERVER";

    public static async Task<int> RunAsync()
    {
        while (await Console.In.ReadLineAsync() is { } line)
        {
            var request = JsonNode.Parse(line)!;
            if (request["id"] == null) continue;
            var result = request["method"]?.ToString() == "model/list"
                ? request["params"]?["cursor"]?.ToString() == "models-2"
                    ? JsonNode.Parse("""{"data":[{"model":"gpt-6.1-sol","displayName":"GPT-6.1 Sol","isDefault":true}],"nextCursor":null}""")
                    : JsonNode.Parse("""{"data":[{"model":"gpt-5.6-terra","displayName":"GPT-5.6 Terra","isDefault":false}],"nextCursor":"models-2"}""")
                : new JsonObject();
            await Console.Out.WriteLineAsync(new JsonObject { ["id"] = request["id"]!.DeepClone(), ["result"] = result }.ToJsonString());
            await Console.Out.FlushAsync();
        }
        return 0;
    }
}
