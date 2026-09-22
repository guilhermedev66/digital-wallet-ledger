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

## Frontend security note (2026-09-22, read-only audit, no blockers)

JWT is stored in `localStorage` (`AuthContext.tsx`), not an httpOnly cookie - a known
XSS-exfiltration tradeoff, but audited with no current exploit path (no
`dangerouslySetInnerHTML`/`.innerHTML`/`eval` anywhere in `src/`, all user-controlled
strings render through JSX's auto-escaping). Acceptable as-is for a no-real-money
portfolio demo. Revisit if a future milestone adds any raw-HTML/markdown rendering
path that could bypass JSX escaping - that's the condition that would turn this into
a real vulnerability, not just an architectural tradeoff.

`mockClient.ts`'s shared `postTransaction` helper (funds/transfers/reversals) now verifies a
replayed idempotency key's parameters actually match the cached result before returning it,
throwing a 409 conflict otherwise - found missing by independent review (2026-09-22) and
fixed same day. Not reachable through the current UI (every call site generates a fresh
`crypto.randomUUID()` key per action), but was a real divergence from the documented
idempotency-conflict contract (see "Idempotency replay semantics" below) that the mock is
supposed to mirror faithfully.

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
  **Real bug found resuming the project (2026-09-22), fixed same session**: `TransferHandler`
  had this backwards — it Debited the source and Credited the destination, which (given the
  formula above) means every transfer would have *increased* the sender's balance and
  *decreased* the receiver's. Caught by hand-tracing the formula against
  `TransferConcurrencyTests.cs`'s already-correct expected balances, not by any test that
  actually ran — the mocked `TransferHandlerTests.cs` unit test had encoded the buggy
  direction as if it were correct (tautological, not independently derived), and the
  Docker-gated integration tests that *did* have the right expected values have still never
  executed against real Postgres locally. Fixed: source is now Credited (money leaving),
  destination is now Debited (money arriving) — matches `SimulateFundingHandler`'s own
  precedent (wallet Debited on a deposit). Any future handler that posts a transfer-shaped
  entry pair must double check its direction against this convention by tracing the formula,
  not by intuition ("debit the sender" reads right in English and is exactly backwards here).
  Independently re-verified by a second session (adversarial review, not a skim): traced all 7
  `TransferConcurrencyTests.cs` cases by hand against the fix (all match), confirmed the
  `MatchesThisTransfer`/`HasMatchingEntry` idempotency-replay check was swapped correctly too
  (a miss there would've made every legitimate replay throw a spurious 409 instead of
  returning the original), and confirmed no other handler/DTO assumed the old direction.
  One follow-up noted, not done yet: the debit-normal formula (`Debit ? +amount : -amount`)
  is hand-duplicated in `EfTransactionRepository.GetAccountBalanceAsync` and
  `ReconciliationEngine.ReconcileAccount` instead of one shared helper - harmless while they
  agree, but reconciliation's whole point is catching drift from "the real formula," and two
  independent copies of it undermines the point if they ever diverge. Extract to a shared
  `LedgerBalanceFormula.Compute(entries)` next time either file is touched.
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
- Never a 403 anywhere in this API - this is a cross-cutting, load-bearing convention
  now (wallet ownership in M1, transfer destination in M3), not a one-off: any "you
  can't do that" case is either a 404 (ownership-scoped, existence hidden from
  non-owners) or a distinct 4xx that isn't about ownership at all (400 for bad
  input/bad destination, 422 for insufficient funds, 409 for idempotency conflict).
  Keep this in mind for M4/M5 - don't introduce a 403 without a real reason to break
  the pattern.
- Idempotency replay semantics (decided in M3, applies to every command carrying an
  IdempotencyKey - SimulateFunding and Transfer so far): replaying the same
  (RequestedByUserId, IdempotencyKey) with matching parameters returns the *original*
  result (never re-posts). Replaying it with *different* parameters is rejected
  (`IdempotencyKeyConflictException`, 409) - never silently applied, never silently
  swallowed. A failed attempt (e.g. insufficient funds) never persists a row, so
  retrying the same key after a failure is correctly treated as a fresh attempt, not
  blocked. `TransactionMatching.HasMatchingEntry` is the shared comparison helper -
  reuse it for any new idempotent command rather than re-deriving matching logic.
- Concurrency control for debiting an account is a row lock (`SELECT ... FOR UPDATE`)
  on that account only, under ReadCommitted isolation - not SERIALIZABLE, not a lock
  on the credited account too (credits can't overdraw, so nothing there needs
  protecting, and never taking two locks per transfer rules out a lock-ordering
  deadlock against a reverse-direction transfer). See
  `EfTransactionRepository.PostTransferIfSufficientFundsAsync`. If a future milestone
  ever needs to debit two accounts in one operation, this reasoning needs revisiting.
  No FK exists from `LedgerEntries.AccountId` to `LedgerAccounts.Id` (only the
  `TransactionId` FK) - if one is ever added for referential integrity, Postgres's
  implicit `FOR KEY SHARE` lock on insert would reopen the reverse-direction-transfer
  deadlock this design currently avoids; both accounts would need locking in a
  canonical order at that point, not just the source.
- Inside any "lock, then check something, then maybe insert" flow (like
  `PostTransferIfSufficientFundsAsync`), order the checks so idempotency detection
  runs before any check that depends on mutable state the first request could have
  already changed (like balance) - not just as a side effect of the unique-constraint
  violation on insert. Found in M3 review: the balance check ran first, so a second
  identical concurrent request against a near-exhausted post-debit balance saw
  InsufficientFunds instead of being recognized as a replay of the first request's
  already-successful transfer. Not fund-corrupting on its own, but a client told its
  transfer failed when it actually succeeded is exactly the setup for a genuine
  double-transfer if it retries with a new key. The regression test that caught this
  needed to fund to *exactly* the combined debit amount - funding with headroom to
  spare doesn't exercise the branch where the ordering bug mattered.
  **This ordering lesson recurred in M4**: `ReverseTransactionHandler`/
  `PostTransferIfSufficientFundsAsync` add a second "first-wins" state check (has this
  original transaction already been reversed, via a filtered unique index on
  `ReversalOfTransactionId`), and it's placed under the same lock, before the balance
  check, before it ever shipped - not discovered by review after the fact this time.
  Any *new* "only one X may exist" check added to this posting path in the future
  should default to this same position (post-lock, pre-balance) rather than trusting
  the DB constraint alone to translate races into the right exception.
- Reversal direction (M4, `ReverseTransactionHandler`): mirroring an entry flips its
  Direction, so the account that LOSES money in the *reversal* - the one that needs
  the balance floor check - is whichever account held the **Debit** entry in the
  *original* (its mirrored entry becomes Credit), not the credited one. Easy to get
  backwards by intuition; derive it from the debit-normal formula above, don't guess.
- Reversal authorization (M4, decided after self-review, not requested explicitly by
  the M4 task handoff): self-service reversal is only allowed when it debits the
  caller's *own* wallet (a recipient voluntarily sending a transfer back, or a wallet
  owner undoing their own SimulatedFunding) - reversing a transaction the *other* way
  (e.g. a sender unilaterally clawing a transfer's proceeds back out of the
  recipient's wallet) requires the admin role. Without this gate, "any party to the
  original transaction can reverse it" would let a sender take money out of someone
  else's wallet without their consent, just by being a party to the original transfer
  - a real authorization gap, not a hypothetical one. See
  `ReverseTransactionHandler.HandleAsync`'s doc comment for the full reasoning.
- `ExceptionHandlerMiddleware` (M7) clears the response - including headers already set
  by earlier middleware - before re-executing its branch on an unhandled exception. A
  header (or anything else) added via `app.Use(...)` earlier in the pipeline does NOT
  automatically survive on a 500 response just because it's registered first; it has to
  be set again inside the `UseExceptionHandler` branch itself (`AddSecurityHeaders` is
  now called from both places in `Program.cs`). Found by actually running the app and
  diffing headers on a normal 401 vs. a forced 500, not by reasoning about middleware
  order alone - the "wraps everything, so it should apply" intuition is wrong here.
- `/api/auth/register` returns 409 with an "email already exists" message (enumerable),
  while `/api/auth/login` returns a generic 401 for both wrong-password and
  unknown-email (never enumerable) - a deliberate, reviewed asymmetry (M7), not an
  inconsistency to fix. Registration-time email enumeration is standard, low-value-to-attacker
  UX (most real signup flows do this) and blocking it would hurt legitimate "why didn't
  my registration work" UX for no real security gain on a no-real-money portfolio demo;
  login-time enumeration (confirming a specific email HAS an account, useful for credential
  stuffing) is the one this project actually guards against, and does. Don't "fix" the
  register asymmetry without re-deciding this tradeoff deliberately.
- Rate limiting (M7) is `[EnableRateLimiting("auth")]` on `AuthController` only (fixed
  window, 10 requests/min, partitioned by remote IP) - not a global limiter. Adding a
  global limiter later needs to account for legitimate same-IP burst traffic on
  wallet/transfer endpoints (e.g. the concurrency integration tests fire many requests
  from one client) - don't copy the "auth" policy's numbers onto a global one without
  reconsidering them.
- CORS (M7): `Cors:AllowedOrigins` in config, empty by default (production has no real
  deploy URL yet, so no browser origin is allowed at all) - `http://localhost:5173`
  (Vite's default dev port) is allowlisted only in `appsettings.Development.json`. When
  a real frontend URL is deployed (M7's remaining "Deployment" item), add it to
  `Cors:AllowedOrigins` in the production config/environment, never switch to
  `AllowAnyOrigin()`.
- `ReverseTransactionHandler`'s admin bypass (M7 security gate finding, fixed): the
  FIRST ownership check (does the caller own the route `WalletId`) originally didn't
  honor `CallerIsAdmin`, only the second one (the affected-account check) did - meaning
  an admin couldn't actually exercise the documented power to reverse a transfer
  between two OTHER users, only ones where they happened to own one of the wallets.
  Both checks now bypass for admin, matching `GetWalletByIdHandler`'s single-check
  pattern. Whenever a handler adds a second ownership check for a different account
  further down (not just the route-level one), double check EVERY check in the chain
  honors `CallerIsAdmin` the same way - it's easy to wire the flag into only the check
  you were focused on and miss an earlier one.
- `Enum.TryParse<T>` on a non-`[Flags]` enum still accepts comma-separated input and
  OR-combines the underlying numbers - e.g. `"SimulatedFunding,Reversal"` (1|2) parses
  to 3, matching no real member. Adding `&& Enum.IsDefined(result)` does NOT fully
  close this: whichever member is numbered 0 is the OR identity, so a combination that
  includes it lands exactly on the OTHER member's own value and passes `IsDefined` too
  - e.g. `"Usd,Brl"` (`Usd = 0`) parses to `Brl` and `IsDefined` says yes, indistinguishable
  from someone just sending `"Brl"`. Verified this precisely with a standalone repro
  before trusting it, since the failure mode is silent and enum-numbering-dependent -
  don't assume `Enum.IsDefined` alone closes a comma-parsing gap without checking which
  member is 0. The actual fix: reject any input containing `,` outright before calling
  `TryParse` at all (see `WalletsController.TryParseDefinedEnum`) - correct regardless
  of enum numbering.
