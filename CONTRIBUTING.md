# Contributing

## Before you open a PR

1. **Run the gate locally** (see [README → CI validation](README.md#ci-validation)):
   ```bash
   dotnet restore
   dotnet build -c Release --no-incremental
   dotnet test ./Atlas.Api.Tests/Atlas.Api.Tests.csproj -c Release
   dotnet test ./Atlas.DbMigrator.Tests/Atlas.DbMigrator.Tests.csproj -c Release
   dotnet test ./Atlas.Api.IntegrationTests/Atlas.Api.IntegrationTests.csproj -c Release
   ```
2. **Open a PR** to `main` or `dev`.
3. **Ensure the CI workflow passes** — `.github/workflows/ci-deploy-dev.yml` (CI and Deploy to Dev) runs the same checks on push/PR; it must pass before merge.

For deployment and secrets, see [README → Deployment](README.md#deployment) and `docs/ci-cd-branch-mapping.md`. To pick and implement the next high-value feature, see `docs/ATLAS-HIGH-VALUE-BACKLOG.md` and `docs/ATLAS-FEATURE-EXECUTION-PROMPT.md`. **For AI agents:** see [AGENTS.md](AGENTS.md) for gate and feature-backlog pointers.
