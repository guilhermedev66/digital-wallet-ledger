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
- [x] Security gate pass on M1 (read-only audit → 2 IMPORTANT findings → fixed with regression tests → revalidated: 45/45 unit tests independently re-run, diff independently reviewed). Details in git history (commit 8e4c6d1) rather than duplicated here.

## M2 — Double-entry ledger
- [x] `Transaction` + `LedgerEntry` model, balance-invariant enforced structurally in `Transaction.Post` (the only construction path - private constructor, `Entries` is a getter-only `IReadOnlyList` with no Add method).
- [x] Simulated funding operation - `POST /api/wallets/{id}/simulate-funding`, posts from a `SystemFunding` `LedgerAccount` seeded per-currency at Api startup (best-effort - won't crash boot if the DB is unreachable, see MEMORY.md).
- [x] Balance derivation query (`GET /api/wallets/{id}/balance`) - unit tests (mocked repo) proving the handler delegates correctly; the real "balance == sum of entries through a real DB" proof is in the Docker-gated integration tests below, not unit tests.
- [x] Domain unit tests actively trying to construct an unbalanced transaction (22 adversarial tests: off-by-one, all-debit/all-credit, zero/negative amounts, mixed currencies, etc. - all correctly rejected).
- [~] Integration tests for the funding→balance flow (proves accumulation across multiple funding calls) - written, same Docker-gated status as M1's integration tests.
- [x] Financial-correctness pass on M2 (2 IMPORTANT findings: torn-read balance query fixed to a single SQL statement before M3's insufficient-funds check depends on it, idempotency dedup gap acknowledged as correctly-deferred M3 work; 2 OPTIONAL items also fixed - SystemFunding filtered unique index, overflow→ArgumentException translation). Revalidated: diff independently reviewed, 53+34 unit tests independently re-run. The single-query balance SQL translation itself still needs the Docker-gated integration tests to prove against real Postgres. Details in git history (commit 4bfb16f) rather than duplicated here.

## M3 — Atomic transfers + idempotency + concurrency
- [x] Wallet-to-wallet transfer command - `POST /api/wallets/{id}/transfer`, source ownership-scoped, same-currency required, self-transfer rejected. Insufficient-funds checked atomically under a row lock (`SELECT ... FOR UPDATE` on the source account, ReadCommitted isolation), not from an earlier separate read.
- [x] Idempotency actually enforced this time (was a no-op header since M2) - unique DB constraint on (RequestedByUserId, IdempotencyKey), replay with matching parameters returns the original result, replay with different parameters is rejected (409), retrofitted onto SimulateFunding too.
- [x] Concurrency integration tests: 7 adversarial scenarios (draining past balance, concurrent identical replay at comfortable and at exactly-exhausted balance, sequential replay match/conflict, replay after a failed attempt, unrelated-transfers-don't-serialize) - written and reviewed line-by-line, same Docker-gated status as the rest of this project.
- [x] Transfer history endpoint - `GET /api/wallets/{id}/history?page=&pageSize=`, paginated, ownership-scoped.
- [x] Financial-correctness pass on M3 (1 real bug found and fixed: idempotency check was running after the balance check inside PostTransferIfSufficientFundsAsync instead of before, so a concurrent identical replay against a near-exhausted post-debit balance got a false InsufficientFunds instead of being recognized as a replay - see MEMORY.md and git history (commit 018817b) rather than duplicated here). Revalidated: diff independently reviewed, fix confirmed correct, 53+57 unit tests independently re-run.
- [x] Second post-hoc financial-correctness fix (found while resuming the project, not part of the original M3 pass): `TransferHandler` had the source/destination entry directions inverted - see MEMORY.md "Balance sign convention" for the full account. Fixed, unit tests corrected, 53+57 unit tests re-run green. This was invisible to every test that ran locally (mocked unit tests just encoded whatever the code did; the Docker-gated integration tests had the correct expected balances all along but have still never executed against real Postgres) - a concrete argument for actually running the integration suite once Docker is available here, not just trusting that it was written carefully.

## M4 — Reversals / reconciliation / activity
- [x] Compensating reversal transactions - `POST /api/wallets/{id}/transactions/{transactionId}/reverse`, no mutation/deletion of posted history, built through the same `Transaction.Post` construction path as every other transaction type. Idempotency-Key header, same replay semantics as M3. A transaction can only be reversed once (DB-enforced via a filtered unique index on `ReversalOfTransactionId`, same two-layer pattern as the idempotency constraint). Self-service reversal is only allowed when it debits the caller's *own* wallet (the recipient voluntarily returning a transfer, or a wallet owner undoing their own SimulatedFunding); reversing a transaction the other way (e.g. a sender clawing back a transfer's proceeds from the recipient) requires the admin role - see MEMORY.md.
- [x] Reconciliation - `GET /api/wallets/{id}/reconciliation` (ownership-scoped, admin bypass) and `GET /api/reconciliation` (global, admin-only). Recomputes every account balance independently from raw `LedgerEntry` rows and re-verifies every touched transaction's per-currency debit/credit balance, straight from raw rows rather than trusting loaded `Transaction` aggregates - see MEMORY.md.
- [x] Activity/history filtering + pagination - extended `GET /api/wallets/{id}/history` with optional `fromUtc`/`toUtc`/`type` filters, same page-based pagination convention as M3 (not switched to cursor-based).
- [x] Financial-correctness self-check on M4 (see MEMORY.md for the reversal-direction and reversal-authorization findings caught during self-review, both fixed before commit). Unit tests: 142 total (53 Domain + 89 Application) independently re-run, all green, 0 build warnings/errors. Integration tests for reversal/reconciliation/history-filter endpoints written, same Docker-gated status as the rest of this project - not run locally.

## M5 — Frontend core experience
- [x] Auth flows (register/login), wallet dashboard, transfer UX, activity list - built against Direction 1 (DESIGN_DIRECTION.md), using `src/api/mockClient.ts` (localStorage-backed) until wired to the real backend.
- [x] API contract reconciled against the real M1-M3 backend (was built against a pre-backend guess that turned out wrong on nearly every endpoint shape - see frontend/API_CONTRACT.md's header for specifics: wrong routes, wrong request/response bodies, idempotency key assumed to be a body field instead of an `Idempotency-Key` header, invented `status`/`memo`/`reversalOfTransactionId` fields the backend doesn't return, cursor pagination instead of page-based). `httpClient.ts` and `mockClient.ts` now both implement the verified real contract.
- [x] Loading/empty/error/disabled/success states; responsive (desktop/tablet/320px); keyboard + a11y basics - verified live in a real browser (Maestri portal), not just code review: empty states (dashboard/transfer/activity, zero wallets), error state (`role="alert"` banner, form stays editable after failure, not stuck disabled), disabled-during-submit, and full golden-path success all confirmed working. Found and fixed one real bug in the process: header overflowed horizontally below ~640px (brand + 3 nav links + search + logout didn't fit one row) - fixed by wrapping nav to its own row rather than hiding labels, since Direction 1 requires labeled nav. Verified no horizontal overflow at 320/375/768/1440px. Command palette (Cmd/Ctrl+K, arrow nav, Enter-to-navigate) confirmed working. Loading skeleton state is code-reviewed but not independently captured live (fires too briefly against the mock client's 350ms delay to reliably screenshot) - lower confidence than the rest of this line.
- [x] Implements the direction chosen in DESIGN_DIRECTION.md.
- [ ] Not yet wired to the real backend end-to-end (mock client only) - `VITE_API_BASE_URL`/`VITE_USE_MOCK_API` control the switch, see `src/api/index.ts`.

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
