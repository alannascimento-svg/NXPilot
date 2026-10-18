// Copyright (c) Nexus XData Tecnologia Ltda — Todos os direitos reservados.
// NXPilot — licenciado sob a NXPilot License 2.0 (Open Core / licenciamento dual).
// Licença: LICENSE.txt (oficial, em português) | LICENSE.en.txt (English version).
// Distribuição comercial somente mediante contrato: comercial.nexus.xdata@gmail.com

using System.Text;

namespace NXPilot.Databricks;

/// <summary>Tipo da instrução, do menos ao mais perigoso (a ordem é usada para pegar o pior).</summary>
public enum SqlStatementKind
{
    Empty,    // só comentário/espaço
    Read,     // SELECT, SHOW, DESCRIBE, EXPLAIN…
    Session,  // USE, SET, RESET — só a sessão
    Unknown,  // palavra que o classificador não conhece: na dúvida, pergunta
    Write,    // INSERT, UPDATE, DELETE, MERGE, COPY INTO
    Schema,   // CREATE, ALTER, DROP, TRUNCATE, VACUUM, RESTORE…
    Admin,    // GRANT, REVOKE, CALL…
}

/// <summary>O que será executado, já classificado — é isto que vai para a aprovação.</summary>
public sealed record SqlStatementReview(string Sql, SqlStatementKind Kind, bool IsDestructive, IReadOnlyList<string> Statements)
{
    public bool IsReadOnly => Kind is SqlStatementKind.Read or SqlStatementKind.Empty;

    /// <summary>Uma linha para log e para o gate: "Databricks SQL [Write]: UPDATE vendas SET …".</summary>
    public string Summary
    {
        get
        {
            var oneLine = string.Join(' ', Sql.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return $"Databricks SQL [{Kind}{(IsDestructive ? ", destrutivo" : "")}]: {(oneLine.Length > 300 ? oneLine[..300] + "…" : oneLine)}";
        }
    }
}

/// <summary>
/// Classificação por regras (sem rede, sem modelo): ignora comentários, textos entre aspas e
/// identificadores com crase, separa as instruções por ';' e olha a primeira palavra de cada
/// uma. Várias instruções → vale a pior. Não é um parser SQL completo, e por isso o que ele não
/// reconhece vira Unknown (nunca Read).
/// </summary>
public static class SqlStatementClassifier
{
    private static readonly Dictionary<string, SqlStatementKind> FirstWord = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SELECT"] = SqlStatementKind.Read, ["SHOW"] = SqlStatementKind.Read, ["DESCRIBE"] = SqlStatementKind.Read,
        ["DESC"] = SqlStatementKind.Read, ["EXPLAIN"] = SqlStatementKind.Read, ["VALUES"] = SqlStatementKind.Read,
        ["TABLE"] = SqlStatementKind.Read, ["LIST"] = SqlStatementKind.Read,
        ["USE"] = SqlStatementKind.Session, ["SET"] = SqlStatementKind.Session, ["RESET"] = SqlStatementKind.Session,
        ["INSERT"] = SqlStatementKind.Write, ["UPDATE"] = SqlStatementKind.Write, ["DELETE"] = SqlStatementKind.Write,
        ["MERGE"] = SqlStatementKind.Write, ["COPY"] = SqlStatementKind.Write,
        ["CREATE"] = SqlStatementKind.Schema, ["ALTER"] = SqlStatementKind.Schema, ["DROP"] = SqlStatementKind.Schema,
        ["TRUNCATE"] = SqlStatementKind.Schema, ["REPLACE"] = SqlStatementKind.Schema, ["RESTORE"] = SqlStatementKind.Schema,
        ["VACUUM"] = SqlStatementKind.Schema, ["OPTIMIZE"] = SqlStatementKind.Schema, ["ANALYZE"] = SqlStatementKind.Schema,
        ["MSCK"] = SqlStatementKind.Schema, ["REFRESH"] = SqlStatementKind.Schema, ["REORG"] = SqlStatementKind.Schema,
        ["CACHE"] = SqlStatementKind.Schema, ["UNCACHE"] = SqlStatementKind.Schema, ["COMMENT"] = SqlStatementKind.Schema,
        ["SYNC"] = SqlStatementKind.Schema, ["UNDROP"] = SqlStatementKind.Schema, ["CLONE"] = SqlStatementKind.Schema,
        ["GRANT"] = SqlStatementKind.Admin, ["REVOKE"] = SqlStatementKind.Admin, ["DENY"] = SqlStatementKind.Admin,
        ["CALL"] = SqlStatementKind.Admin, ["ADD"] = SqlStatementKind.Admin, ["EXECUTE"] = SqlStatementKind.Admin,
    };

    /// <summary>Apagam ou sobrescrevem dados/objetos.</summary>
    private static readonly HashSet<string> Destructive = new(StringComparer.OrdinalIgnoreCase)
        { "DROP", "TRUNCATE", "DELETE", "REPLACE", "VACUUM", "RESTORE", "OVERWRITE", "REVOKE", "DENY" };

    /// <summary>Palavras que, dentro de um WITH/FROM, transformam a leitura em escrita.</summary>
    private static readonly HashSet<string> WriteInside = new(StringComparer.OrdinalIgnoreCase)
        { "INSERT", "UPDATE", "DELETE", "MERGE" };

    public static SqlStatementReview Classify(string sql)
    {
        var statements = new List<string>();
        var worst = SqlStatementKind.Empty;
        var destructive = false;
        foreach (var words in SplitStatements(sql, statements))
        {
            if (words.Count == 0) continue;
            var kind = FirstWord.TryGetValue(words[0], out var k) ? k : SqlStatementKind.Unknown;
            // WITH cte AS (…) SELECT / FROM t SELECT: leitura, a menos que traga escrita dentro.
            if (words[0].Equals("WITH", StringComparison.OrdinalIgnoreCase) || words[0].Equals("FROM", StringComparison.OrdinalIgnoreCase))
                kind = words.Any(WriteInside.Contains) ? SqlStatementKind.Write : SqlStatementKind.Read;
            destructive |= kind >= SqlStatementKind.Write && words.Any(Destructive.Contains); // EXPLAIN DROP… é Read
            if (kind > worst) worst = kind;
        }
        return new SqlStatementReview(sql, worst, destructive, statements);
    }

    /// <summary>
    /// Lê o SQL caractere a caractere: pula comentários (-- e /* */ aninhados), textos ('…', "…"
    /// com \ e aspas dobradas) e `identificadores`; corta em ';'. Devolve as palavras de cada instrução.
    /// </summary>
    private static IEnumerable<List<string>> SplitStatements(string sql, List<string> statements)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        var current = new StringBuilder();
        void EndWord() { if (word.Length > 0) { words.Add(word.ToString()); word.Clear(); } }

        for (var i = 0; i < sql.Length; i++)
        {
            var c = sql[i];
            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                EndWord();
                while (i < sql.Length && sql[i] != '\n') i++;
                current.Append('\n');
                continue;
            }
            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                EndWord();
                var depth = 0;
                for (; i < sql.Length; i++)
                {
                    if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*') { depth++; i++; }
                    else if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/') { depth--; i++; if (depth == 0) break; }
                }
                current.Append(' ');
                continue;
            }
            if (c is '\'' or '"' or '`')
            {
                EndWord();
                var start = i;
                for (i++; i < sql.Length; i++)
                {
                    if (sql[i] == '\\' && c != '`') { i++; continue; }
                    if (sql[i] == c)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == c) { i++; continue; } // aspas dobradas
                        break;
                    }
                }
                current.Append(sql, start, Math.Min(i, sql.Length - 1) - start + 1);
                words.Add(c == '`' ? "`id`" : "'txt'"); // ocupa o lugar, mas nunca casa com palavra-chave
                continue;
            }
            if (c == ';')
            {
                EndWord();
                if (current.ToString().Trim().Length > 0) statements.Add(current.ToString().Trim());
                current.Clear();
                yield return words;
                words = new List<string>();
                continue;
            }
            current.Append(c);
            if (char.IsLetterOrDigit(c) || c == '_') word.Append(c);
            else EndWord();
        }
        EndWord();
        if (current.ToString().Trim().Length > 0) statements.Add(current.ToString().Trim());
        yield return words;
    }
}
