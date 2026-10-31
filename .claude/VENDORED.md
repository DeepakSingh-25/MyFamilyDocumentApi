# Agent tooling in `backend/.claude/` — provenance

Copied on 2026-10-16 from the reference API `D:\Working\work\API\.claude`, which vendored it from
upstream (via the OM.HRMS API checkout).

## Vendored (do not edit — project rules go in `../CLAUDE.md`)

| Component | Upstream | Version | Licence |
|---|---|---|---|
| `skills/` (47), `agents/` (10 generic: `api-designer`, `build-error-resolver`, `code-reviewer`, `devops-engineer`, `dotnet-architect`, `ef-core-specialist`, `performance-analyst`, `refactor-cleaner`, `security-auditor`, `test-engineer`), `rules/` (10), `templates/` (5) | https://github.com/codewithmukesh/dotnet-claude-kit | 0.12.0, commit `23300897f4d1a276bc475e373ecfe886e43dd7c5` | MIT — `LICENSE-dotnet-claude-kit` |

Kit examples use placeholder names like `MyApp.Api` / `MyApp.Domain` — those are upstream examples,
not this project.

**One exception:** `rules/architecture.md` is **rewritten for this repo** (controllers + MediatR +
repositories, `Documents.*` layers, per-user ownership). The upstream version forbids the repository
pattern, which is wrong here, and the file is `alwaysApply`.

### The kit is opinionated in ways this repo is not
The kit prefers Minimal APIs, vertical slices, no repository pattern, no MediatR, `Result<T>`,
file-scoped namespaces and CPM. This API uses controllers + MediatR + repositories and block-scoped
namespaces, same as the reference. **Project conventions win.** Use the kit skills for techniques
(EF Core, auth, testing, security scanning, resilience), not as a mandate to restructure.

## Written for this repo (edit freely)

`agents/backend-architect.md`, `backend-coder.md`, `backend-code-reviewer.md`,
`backend-security-auditor.md`, `backend-tester.md`, `database-migration-specialist.md`,
`rules/architecture.md`, `settings.json`, this file, `../CLAUDE.md`.

## Not copied from the reference

- `hooks/` (bash) — were copied-but-unwired in the reference too. `rules/hooks.md` still describes
  them; ignore it until hooks are added.
- `hooks-ps/` (`gate-agent.ps1`, `db-write-guard.ps1`, `session-start.ps1`, `statusline.ps1`) and the
  hook / status-line / `model` entries of the reference `settings.json`. The DB guard existed because
  the reference uses a shared LAN database; this project uses a local one and the agents ask before
  any DB write instead.
- `.mcp.json` (Roslyn navigator MCP server). `rules/agents.md` mentions its tools (`find_symbol`,
  `get_diagnostics` …); they are unavailable unless you install `cwm-roslyn-navigator` and add the
  `.mcp.json`.
- `launch.json`, `docs/specs/` (reference-specific).
