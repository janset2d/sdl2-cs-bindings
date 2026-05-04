# CLAUDE.md

The canonical agent contract for Claude Code (claude.ai/code) and other LLM assistants lives in [AGENTS.md](AGENTS.md). Read it first — it carries the approval gate, communication preferences, settled strategic decisions, build-host reference pattern, and review protocol.

## Quick Pointers

- [AGENTS.md](AGENTS.md) — operating rules, approval gate, build-host reference pattern
- [docs/onboarding.md](docs/onboarding.md) — project overview, repo layout, glossary
- [docs/plan.md](docs/plan.md) — current status, active phase, roadmap
- [docs/phases/README.md](docs/phases/README.md) — phase index
- [docs/README.md](docs/README.md) — full documentation map
- [docs/playbook/local-development.md](docs/playbook/local-development.md) — fresh-clone setup, native build, troubleshooting

## Common Commands

```pwsh
# tools.cs is the canonical dev-orchestration entry point (file-based .NET 10 app, forwards to Cake):
dotnet run --file tools.cs -- build --target Info      # Cake forwarder (passthrough)
dotnet run --file tools.cs -- build --tree
dotnet run --file tools.cs -- setup                    # local-dev feed bootstrap (--source=local|remote-github|remote-nuget)
dotnet run --file tools.cs -- ci-sim                   # mini CI replay (9-step pipeline, per-step logs)

# Direct Cake invocations (CI debugging / target discovery only):
dotnet run --project build/_build -- --tree
dotnet run --project build/_build -- --target Info

# Build-host regression suite (TUnit on Microsoft.Testing.Platform)
dotnet test --project build/_build.Tests/Build.Tests.csproj -c Release --framework net10.0

# Managed-only build (skip native pipeline)
dotnet build src/SDL2.Core/SDL2.Core.csproj
```

> The Cake build host (`build/_build/`) is a CI-only production pipeline. Day-to-day dev orchestration lives in `tools.cs` at the repo root. See AGENTS.md "Build-Host Reference Pattern" for architecture details and `docs/playbook/local-development.md` for the full local-dev workflow.
