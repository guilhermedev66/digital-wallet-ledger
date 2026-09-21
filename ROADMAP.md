# Roadmap

Status legend: `[ ]` not started, `[~]` in progress, `[x]` done.

## M0 — Architecture / project skeleton
- [x] Reality check (see MEMORY.md) — repo init, tooling, actual multi-agent topology.
- [x] .NET solution skeleton (Domain/Application/Infrastructure/Api + 3 test projects), builds clean.
- [x] React + TypeScript + Vite frontend skeleton.
- [x] docker-compose.yml (PostgreSQL), .env.example, .gitignore.
- [x] GitHub Actions CI skeleton (backend build/test, frontend lint/typecheck/build, secret scan).
- [x] ARCHITECTURE.md — financial invariants, ledger model, auth model.
- [x] Initial commit (local; GitHub remote still pending a decision — see below).
- [x] UX/visual research and DESIGN_DIRECTION.md — Direction 1 "Ledger-as-Instrument" chosen.

## M1 — Identity + Wallet foundation
- [x] User registration/login, password hashing (ASP.NET Core Identity's PasswordHasher<T>), JWT issuance (HS256, fails fast on a signing key under 256 bits).
- [x] `LedgerAccount`/Wallet creation tied to an authenticated user (owner id from JWT `sub` claim only), currency selection.
- [x] Ownership-scoped read endpoints (list my wallets, get my wallet by id — **404 only, never 403**, for both "doesn't exist" and "not yours"; admin role bypasses ownership, see ARCHITECTURE.md AuthN/AuthZ).
- [x] EF Core PostgreSQL migrations generated (schema only — not yet run against a real Postgres locally, see known environment blockers).
- [~] Testcontainers-backed integration tests + authorization matrix tests (anonymous, owner, non-owner, admin) — written, can't run locally (no Docker daemon), needs verification once Docker/CI is available.

## M2 — Double-entry ledger
- [ ] `Transaction` + `LedgerEntry` model, balance-invariant enforced in Domain constructors.
- [ ] Simulated funding operation (clearly labeled demo-only, posts from a system funding account).
- [ ] Balance derivation query + unit tests proving balance == sum of entries.
- [ ] Domain unit tests actively trying to construct an unbalanced transaction (must be impossible).

## M3 — Atomic transfers + idempotency + concurrency
- [ ] Wallet-to-wallet transfer command, single DB transaction, insufficient-funds handling.
- [ ] Idempotency key support with unique constraint + replay test.
- [ ] Concurrency integration tests: concurrent transfers against real PostgreSQL (double-spend, races).
- [ ] Transfer history endpoint (paginated, ownership-scoped).

## M4 — Reversals / reconciliation / activity
- [ ] Compensating reversal transactions (no mutation/deletion of posted history).
- [ ] Reconciliation endpoint/job: recompute balances from raw entries, flag drift.
- [ ] Activity/history filtering + pagination.

## M5 — Frontend core experience
- [ ] Auth flows (register/login), wallet dashboard, transfer UX, activity list.
- [ ] Loading/empty/error/disabled/success states; responsive (desktop/tablet/~320px); keyboard + a11y basics.
- [ ] Implements the direction chosen in DESIGN_DIRECTION.md.

## M6 — Reporting / operational polish
- [ ] Reconciliation view in UI, transaction detail view, filtering.
- [ ] Only if it adds real portfolio value — no scope invented for its own sake.

## M7 — Security / production hardening / deployment
- [ ] Full security gate pass (see CLAUDE.md), dependency + secret scan clean.
- [ ] Rate limiting, CORS, security headers, prod error handling (no stack traces to client).
- [ ] Deployment; production security smoke test against the real deployed URL.

## Known environment blockers (see MEMORY.md for detail)
- Docker CLI is not available in this WSL distro (Docker Desktop WSL integration not
  enabled) — blocks running docker-compose / Testcontainers **locally** until the
  user enables it. CI (GitHub Actions) has Docker natively and is unaffected.
