# NXPilot.GitRepos

Clone, pull, commit, push e pull request no **GitHub** e no **Azure DevOps Repos**, com camada de serviço.
Não tem dependências. As operações de repositório são feitas pelo `git` instalado na máquina, e a REST API entra só no que o git não faz: listar repositórios e abrir pull request.

## Camadas

| Pasta | Tipo | Papel |
|---|---|---|
| `Models/` | `GitRepository`, `GitCredential`, `PullRequestInfo`… | `GitRepository.Parse` entende URLs do GitHub, de `dev.azure.com` e de `*.visualstudio.com`. |
| `Git/` | `GitCli` | Roda o git. O token não vai na linha de comando nem no `.git/config`: vai em `GIT_CONFIG_*`, como cabeçalho limitado ao host do repositório. Também nunca abre janela de login. |
| `Api/` | `GitHubApi`, `AzureReposApi` (`IGitHostApi`) | Lista repositórios e abre pull request. |
| `Services/` | `IGitRepoService` / `GitRepoService` | Cada operação passa por um `IGitOperationApprover`. O padrão (`LocalOnlyGitApprover`) só libera o que fica na máquina. |

## Uso

```csharp
var git = new GitRepoService(new StaticGitCredentials(gitHubToken: tokenGh, azureDevOpsToken: pat), approver);

var repo = GitRepository.Parse("https://dev.azure.com/empresa/Dados/_git/pipelines");
var dir  = await git.CloneOrPullAsync(repo, @"C:\Repos\pipelines");
await git.CreateBranchAsync(dir, "feature/ajuste-vendas");
// … o agente edita os notebooks/arquivos …
await git.CommitAsync(dir, "Ajusta a agregação de vendas");
await git.PushAsync(dir);                        // nunca usa --force
await git.CreatePullRequestAsync(dir, "Ajuste de vendas", "Detalhes…");
```

O push nunca é forçado, e o pull só aceita fast-forward. A ponte com o Databricks (`DatabricksReposClient` + `GitDatabricksSync`, no NXPilot) faz o Git folder do workspace mudar para a branch enviada.
