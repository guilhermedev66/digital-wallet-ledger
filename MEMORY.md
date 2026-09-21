# Project Memory

Only what's expensive to relearn. See ROADMAP.md for status, ARCHITECTURE.md for the
financial/domain model and why.

## Environment reality (2026-09-21)

- This WSL distro has no `docker` CLI (Docker Desktop's WSL integration isn't
  enabled for it). `docker-compose` / Testcontainers work in CI but not locally
  until the user enables the integration. Don't assume Docker is usable here
  without checking first.
- `dotnet new sln` on this SDK (10.0.401) generates `.slnx`, not `.sln`. Build with
  `dotnet build WalletLedger.slnx`.
- `dotnet` in this shell is a wrapper (`~/.local/bin/dotnet`) that execs the Windows
  `dotnet.exe`. A locally-run ASP.NET Core app (`dotnet run`) therefore binds to
  Windows' network stack, not WSL's — plain WSL `curl` gets connection refused
  (`status:000`) against `localhost`. Use `curl.exe` (via WSL interop) to smoke-test
  a locally running app instead.
- `Npgsql.EntityFrameworkCore.PostgreSQL` tends to lag `Microsoft.EntityFrameworkCore`
  patch releases, which silently wins an MSBuild reference conflict in Npgsql's favor
  (older `Microsoft.EntityFrameworkCore.Relational`) even when a newer version is
  otherwise pulled in — showed up as an MSB3277 warning, not an error, so it's easy to
  ship without noticing. Fix: pin `Microsoft.EntityFrameworkCore.Relational` directly
  to match whatever version `Microsoft.EntityFrameworkCore` is on.
- `dotnet user-secrets` works fine here for local JWT signing key / connection string
  (no Docker/network dependency) — this is the established pattern for local secrets
  in this repo; never put real values in `appsettings*.json`.

## Multi-agent topology (actual, not aspirational)

The original build brief assumed a Maestro roster with named specialist roles
(Codex Backend, Codex QA, Antigravity, Security QA, a Shell-only worker). On
inspection, none of that existed: `ListAgents` showed two peer sessions, both
plain Claude Code / Sonnet 5, no pre-assigned roles, no Codex or Antigravity
connected (a leftover `.codex/config.toml` in the duplicated workspace is not a
live worker). Roles were assigned ad hoc based on what's real — see whichever
session picked up backend vs. frontend/QA at the time; check `ListAgents` fresh
each session rather than trusting this note to stay current on *who*, only on
*the fact that roles must be verified, never assumed*.

## Architecture decisions

- No generic `IRepository<T>`. See ARCHITECTURE.md "Why not Repository-per-aggregate".
- Balance is always derived from `LedgerEntry` rows, never a mutable column. This is
  the non-negotiable center of the project — don't let a future milestone add a
  shortcut mutable balance for convenience.
- Balance sign convention (not written down in ARCHITECTURE.md, decided in M2): every
  `LedgerAccount` — wallets and the `SystemFunding` account alike — uses one uniform
  debit-normal formula, `balance = sum(Debit entries) - sum(Credit entries)`. No
  per-account-type sign flip. Consequence: a `SystemFunding` account's own balance goes
  increasingly negative over time since it only ever gives money away via
  SimulatedFunding (debit the wallet, credit the funding account) — that's intentional,
  not a bug, and that account's balance is never surfaced to a caller. If a future
  milestone introduces an account type that should behave credit-normal (a real
  liability/revenue account), it needs its own explicit sign handling — don't assume
  this formula generalizes.
- `Transaction.Entries` is a getter-only `IReadOnlyList<LedgerEntry>` backed by a
  private field, with no Add/setter — EF Core needs
  `builder.Navigation(t => t.Entries).UsePropertyAccessMode(PropertyAccessMode.Field)`
  in `TransactionConfiguration` to materialize it; without that it can throw a
  "navigation has no setter" error at model-build time.
- Any `IWalletRepository`/`ITransactionRepository`-dependent startup step (e.g. seeding
  the `SystemFunding` accounts in `Program.cs`) must be wrapped in try/catch and log a
  warning rather than throw — this WSL distro has no reachable Postgres, and letting a
  seeding failure crash the whole process at boot would make it impossible to even
  smoke-test routing/auth locally, which M1 had relied on. Established pattern: log via
  `app.Logger`, keep serving every other endpoint.
