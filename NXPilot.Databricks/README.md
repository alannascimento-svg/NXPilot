# NXPilot.Databricks

Cliente .NET da **SQL Statement Execution API** do Databricks (SQL Warehouse), com camada de serviço.
Não tem dependências: usa só `HttpClient` e `System.Text.Json`.

## Camadas

| Pasta | Tipo | Papel |
|---|---|---|
| `Models/` | `DatabricksConnection`, `SqlQueryResult`, `SqlParameter`… | Dados. O token não aparece em `ToString` nem em exceções. |
| `Sql/` | `SqlStatementClassifier` | Classifica o SQL (Read, Session, Write, Schema, Admin ou Unknown) sem rede. |
| `Client/` | `DatabricksSqlClient` | As 4 chamadas REST (`/api/2.0/sql/statements`), uma para uma. |
| `Services/` | `IDatabricksSqlService` / `DatabricksSqlService` | Classifica, **pede aprovação**, executa, espera, pagina e cancela no warehouse ao desistir. |

## Uso

```csharp
var conn = new DatabricksConnection("adb-123.azuredatabricks.net", token, "/sql/1.0/warehouses/abc123");
using var sql = new DatabricksSqlService(conn);             // aprovador padrão: somente leitura

var r = await sql.QueryAsync("SELECT * FROM main.vendas.pedidos WHERE ano = :ano",
    [new SqlParameter("ano", "2026", "INT")]);
Console.WriteLine($"{r.Rows.Count} linhas; 1ª coluna = {r.Value(0, "id")}");

// Escrita, DDL e GRANT passam pelo aprovador (no NXPilot: DecisionGate + você).
await sql.ExecuteAsync("UPDATE main.vendas.pedidos SET status = 'ok' WHERE id = :id", [new("id", "42", "INT")]);
```

- Uma instrução por vez, porque a API não executa scripts separados por `;`.
- `SqlExecutionOptions.MaxRows` (padrão 10 000) e `Timeout` (padrão 10 min). Quando o tempo esgota, a instrução também é cancelada no warehouse.
- Use parâmetros nomeados (`:nome`) em vez de concatenar valores no SQL. Para nomes de tabela, use `DatabricksSqlService.Quote`.
- `DatabricksConnection.FromEnvironment()` lê `DATABRICKS_HOST`, `DATABRICKS_TOKEN` e `DATABRICKS_WAREHOUSE_ID`.
