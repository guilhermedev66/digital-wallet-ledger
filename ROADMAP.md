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
- [x] Reconciliation view - new `/reconciliation` page (nav + command palette entry), per-wallet: projected vs. independently-recomputed balance, drift, and any structurally-unbalanced transactions found (should always be empty given `Transaction.Post`'s invariant - shown if not, as the defense-in-depth signal it's meant to be). Chosen because ARCHITECTURE.md explicitly frames reconciliation as "the project's proof that the ledger is trustworthy," not an afterthought - real portfolio value, not scope padding.
- [x] Transaction reversal action - "Reverse" button per eligible Activity row (hidden for `Reversal` rows and, best-effort from the currently-loaded page, for already-reversed ones - the backend's 409 is the real guard either way), inline confirm step, calls the M4 `POST .../reverse` endpoint. `mockClient.ts` mirrors the backend's self-service-only-when-it-debits-your-own-wallet authorization rule.
- [ ] Transaction detail view, history filtering (fromUtc/toUtc/type) UI - not built; the backend supports both (see API_CONTRACT.md) but neither had enough remaining scope/time this session to justify the UI work - fair candidate for a future session.
- Verified live in a real browser: fund → reverse → balance correctly returns to pre-funding value → reconciliation reports "Balanced," no console errors. Found and fixed one more responsive bug in the process: the header (now 4 nav items) overflowed at 768px (tablet) even though 320/375px were already fixed - the desktop single-row breakpoint didn't account for the added "Reconciliation" nav item. Raised the wrap threshold from 640px to 860px; re-verified no overflow at 320/375/640/768/860/900/1024/1440px.

## M7 — Security / production hardening / deployment
- [x] Full security gate pass (see CLAUDE.md), dependency + secret scan clean - read-only,
  covering everything not already hit by the two narrower audits earlier this session (M1-M3
  backend, M5 frontend): dependency scan (`dotnet list package --vulnerable
  --include-transitive` across all 7 backend projects, `npm audit` incl. devDependencies - 0
  vulnerable packages either side), full-git-history secret scan (45 commits, `git log -p
  --all` grepped for key/credential/connection-string patterns - only the known-non-secret
  throwaway dev password and test fixture password turned up, no `.env`/`.pem`/`.key` ever
  committed; CI's `gitleaks-action` is configured but hasn't run yet, no GitHub remote), a
  full RBAC/tenant-isolation matrix (anonymous/owner/non-owner/admin) traced across every
  endpoint including M4's reversal/reconciliation surface, SQL injection (one raw-SQL call
  site, parameterized, not injectable), CSRF (N/A, stateless Bearer auth), upload
  authorization (N/A, no upload feature). 1 IMPORTANT finding (`ReverseTransactionHandler`'s
  admin bypass only covers the second of two ownership checks, so an admin can't actually
  exercise the documented cross-user reversal power yet), 6 OPTIONAL (mostly test-coverage
  gaps and pre-deployment items correctly deferred to the Deployment line below). Handed off
  for fixing (see MEMORY.md); revalidate here once that lands.
  **Revalidated - both handed-off findings fixed**: `ReverseTransactionHandler`'s first
  ownership check now also honors `CallerIsAdmin` (regression test: admin reverses a transfer
  between two OTHER users, neither wallet their own - passes). The OPTIONAL `Enum.TryParse`
  comma-quirk fix (item 3) needed a correction mid-fix - the first attempt (`Enum.IsDefined`
  alone, as literally suggested in the handoff) turned out NOT to fully close it: verified
  with a standalone repro that `"Usd,Brl"` still parses as `Brl` and passes `IsDefined`, since
  `Usd=0` is the bitwise-OR identity - only combinations that land on an undefined numeric
  value get caught that way. Shipped an explicit comma-rejection instead (`TryParseDefinedEnum`
  in `WalletsController.cs`), verified against both the originally-cited case and the one that
  would've slipped through. 143 unit tests green, 0 build warnings. Independently re-confirmed
  by the orchestrating session: solution rebuilt clean, 53+90 unit tests independently re-run,
  both diffs reviewed line-by-line.
- [x] Rate limiting, CORS, security headers, prod error handling (no stack traces to client) -
  fixing a read-only audit's findings (handoff from a peer session, not a fresh audit this
  session): rate limiting on `/api/auth/login`/`register` (`[EnableRateLimiting("auth")]`,
  fixed-window, 10/min per remote IP - `Microsoft.AspNetCore.RateLimiting`, no extra NuGet
  dependency), a global exception handler (`UseExceptionHandler` + `Results.Problem()` -
  RFC 7807 body, never the exception's Message/StackTrace), a restrictive CORS policy
  (`Cors:AllowedOrigins` from config, empty/closed in production until a real deploy URL
  exists, never `AllowAnyOrigin()`/credentials), and two baseline response headers
  (`X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`). `/api/auth/register`'s
  409-on-duplicate-email (enumerable, unlike login's generic 401) was reviewed and kept
  deliberately - see MEMORY.md for the reasoning. All four verified against the real running
  app (not just unit tests) with `dotnet run` + `curl.exe`, since Postgres isn't reachable
  locally either way - found and fixed a real gap doing this: headers set by earlier
  middleware were silently dropped on a 500 response because `UseExceptionHandler` clears the
  response before re-executing its branch; confirmed fixed live before writing the regression
  test. 142+ unit tests still green, 0 build warnings. Integration regression tests written
  for all four (rate limit 429, clean ProblemDetails + headers survive an exception, CORS
  allow/deny), same Docker-gated status as the rest of this project - not run locally.
- [ ] Deployment; production security smoke test against the real deployed URL.

## M8 — Frontend visual redesign
- [x] UX research (Antigravity, live browsing of Inspora/Refero Styles/Spell) and direction
  selection - Direction 1 "The Ledger Terminal" chosen jointly by the Orchestrator and
  Claude — Frontend/UI, documented with full rationale and rejected alternatives in
  DESIGN_DIRECTION.md.
- [x] `tokens.css` rewritten to the selected palette (void/carbon/graphite surfaces,
  ember/amber/emerald accents reserved for money-in-motion/pending/balanced states only);
  dark is the unconditional default, light is a separately-designed override, not an
  inversion. Dead Vite-template `index.css` deleted.
- [x] Dashboard, Transfer, Activity, and Reconciliation pages redesigned against Direction 1
  - wallets shown as "Account Nodes" (`WalletCard`, monospaced UUID + copy action, tabular
  balance, real telemetry), Transfer shows an explicit ledger preview of both legs before
  commit, Activity shows both Debit/Credit legs per transaction (not just the caller's
  side), Reconciliation gained a summary strip (accounts-balanced / unbalanced-transactions
  counts). New `ThemeToggle`/`useTheme` (persisted, pre-hydration FOUC guard in
  `index.html`), `CopyButton`, `lib/currency.ts`.
- [x] Currency UX: wallet creation exposes all 7 backend-supported currencies (USD, BRL,
  EUR, GBP, CHF, CAD, AUD - JPY deliberately excluded, see `api/types.ts` comment); dashboard
  groups wallets by currency and totals only within a currency group, never across ones - no
  invented FX, no fake aggregate balance.
- [x] Rendered-app visual QA (live browser via Maestri portal, both themes, 320/375/1440px,
  full deposit → transfer → activity → reconciliation flow with real mock-client data) -
  found and fixed 2 real issues: `WalletCard`'s deposit toggle and its own inline form both
  showed a "Cancel" button simultaneously (confusing, now the toggle hides while the form is
  open); `ActivityPage`'s Debit/Credit leg value colors were inverted against
  DESIGN_DIRECTION.md's documented convention (Debit = neutral white ink, Credit = emerald -
  the code had it backwards). Verified no horizontal overflow at 320/375/1440px, disabled-state
  styling (0.55 opacity) confirmed applied correctly, currency-mismatch and
  insufficient-funds validation both confirmed live, reversal-hiding for `Reversal`/
  already-reversed rows confirmed live, light/dark computed colors spot-checked against the
  exact hex values in DESIGN_DIRECTION.md's palette tables (all matched).
- [x] Independent functional regression QA (Codex QA, read-only): no confirmed regressions -
  transfer currency enforcement, idempotency-key reuse, reversal eligibility, and
  amount/balance formatting all consistent with `mockClient.ts` and the real backend. One
  real finding, fixed: `frontend/API_CONTRACT.md` still documented the pre-M7 two-currency
  (`USD`/`BRL`) union instead of the current 7-currency one.
- [x] Light security sanity check (not a full SECURITY GATE pass - this milestone touches no
  API/auth/authz/data/tenant-isolation surface, only frontend presentation): no
  `dangerouslySetInnerHTML`/`eval`, no hardcoded secrets, no `.env`/credential files touched;
  the new pre-hydration theme script in `index.html` only reads its own localStorage key,
  wrapped in try/catch, no innerHTML.
- [x] `npm run build` and `npm run lint` clean (0 errors; pre-existing `set-state-in-effect`
  warnings on files this milestone didn't touch, not a new regression).
- [ ] Deployment - deliberately not done yet; local dev server left running for the user to
  inspect before any GitHub/Neon/Render/Vercel decision.

## M8.1 — Frontend structural redesign (post-M8 QA rejection)
- [x] Reality check: M8's "Direction 1" implementation (commit 45b4c1c) was visually themed
  (new palette/typography) but structurally unchanged - still generic bordered-box CRUD
  layouts. Rejected as not a real product redesign; M8.1 is the structural rebuild, picked
  back up mid-flight this session after an interruption (see MEMORY.md).
- [x] Antigravity research pass (`DESIGN_BRIEF_M8.1.md`) - live browsing of
  styles.refero.design, spell.sh, and inspora.design again (15 concrete reference-to-
  application entries), producing the "Integrated Accounting Workbench" brief: Transfer as a
  5-stage directional pipeline (Source -> Amount -> Destination -> Double-Entry Manifest ->
  Commit), wallets as "Account Nodes," zero-shadow 4-tier surface stepping, 4 new
  Spell-pattern components adopted (`Kbd`, `FlowButton`, `LabelInput`, `BarsSpinner`;
  `CopyButton` already existed from M8).
- [x] Structural redesign implemented across Dashboard (currency clusters, never summed
  across currencies), Transfer (5-stage pipeline + sealed receipt), Activity (dual-leg
  journal with tree-bracket counterparty rows), Reconciliation (forensic comparison matrix +
  integrity banner), AppShell (segmented nav rail, terminal identity badge), WalletCard
  (Account Node anatomy: status row, balance block, in-place deposit drawer with preset
  chips).
- [x] First Antigravity visual QA pass (`ANTIGRAVITY_QA_M8.1.md`): verdict PASS with
  distinction, 0 BLOCKER, 1 IMPORTANT (Transfer stayed single-column above ~1200px instead of
  the brief's two-pane split view), 4 OPTIONAL (deferred per protocol - only BLOCKER/IMPORTANT
  get fixed mid-milestone).
- [x] IMPORTANT finding fixed: `.pipelineGrid` in `TransferPage.module.css` switches to a
  sticky 2-pane CSS grid (left: source/amount/destination stages, right: manifest + commit,
  pinned) - gated to >=1180px rather than the brief's nominal 1024px, because live-testing
  found real horizontal overflow in the 1024-1179px range with a fixed 420px right column;
  documented in the CSS comment itself, not just here.
- [x] Antigravity revalidation (targeted, not a full re-QA): confirmed zero overflow at
  1024px and 1179px (single column), correct sticky 2-pane grid at 1180px and 1440px, both
  dark/light theme tokens correct at the 1440px split view. 0 BLOCKER, 0 IMPORTANT, 0 new
  OPTIONAL.
- [x] Codex QA independent functional regression review: 0 regressions across transfer
  guards (currency mismatch, insufficient funds), idempotency key lifecycle, reversal
  eligibility gating, debit/credit leg coloring convention, reconciliation drift fields, and
  currency-cluster totals; `frontend/API_CONTRACT.md` confirmed still accurate. One
  pre-existing (not new) observation raised: `reversedTransactionIds` is still best-effort
  current-page-only (already documented under M6 above) - not a regression, not fixed here.
- [x] Regression gate: frontend lint (oxlint, 0 errors, only pre-existing
  `set-state-in-effect` warnings), typecheck + production build both clean, backend build
  clean (0 warnings/errors, confirming the frontend-only diff didn't touch backend), backend
  unit tests re-run for due diligence (54 Domain + 90 Application, all green). Postgres-gated
  integration tests not re-run - no backend files changed this milestone, last confirmed
  green 2026-09-23 (see MEMORY.md). No frontend test runner is configured in this repo
  (unchanged from every prior milestone - nothing to run).
- [x] Light security sanity check (not a full SECURITY GATE - this milestone touches no
  API/auth/authz/data/tenant-isolation surface, frontend presentation only): diff scanned
  clean for `dangerouslySetInnerHTML`/`.innerHTML`/`eval`/hardcoded secrets/raw token
  storage - none found.
- [ ] Deployment - still deliberately not done; local dev server left running on
  `localhost:5173` for inspection before any GitHub/Neon/Render/Vercel decision.

## M8.2 — Source-driven frontend rebuild (post-M8.1 evidence audit)
- [x] Evidence audit (read-only, before any edit): confirmed no real Refero DESIGN.md,
  no real Spell UI source, and no real Inspora asset had ever been incorporated in M8/M8.1 -
  all "Spell-pattern" components (`Kbd`, `CopyButton`, `FlowButton`, `LabelInput`,
  `BarsSpinner`) were hand-rolled reimplementations, never installed. `package.json`/
  `package-lock.json` had zero diff across both milestones. Full findings reported inline
  in-session (not a separate file) before M8.2 scope was defined.
- [x] Real design source retrieved and saved to disk (not AI summaries):
  `docs/design/refero/brex-design.md` (Brex's actual DESIGN.md, verbatim, from Refero
  Styles - single coherent style, not a 5-source mix); `docs/design/spell/*.json` (4 real
  Spell UI registry artifacts - `badge`, `copy-button`, `pop-button`, `kbd` - fetched
  byte-for-byte from `spell.sh/r/<name>.json`, the same endpoint their own
  `shadcn@latest add` installer uses); `docs/design/inspora-references.md` (3 composition
  references, explicitly reference-only, no assets incorporated).
- [x] Spell UI integration decision: every real Spell component is a Tailwind+Radix+CVA
  shadcn registry item; this project has none of those. Rather than bootstrapping a second
  styling system project-wide just to run 4 components verbatim, chose to adapt the real
  source's preserved logic/API (variant/size axes, the icon-crossfade mechanic, the
  border-thins-plus-scaleY press mechanic, the key-symbol lookup table) into this project's
  existing CSS Modules + `tokens.css`, documented per-component (official URL, preserved
  logic, changes made, final files) in `docs/design/spell/ADAPTATIONS.md`. New `PopButton`
  component; `Badge`/`CopyButton`/`Kbd` rewritten in place on the real source.
- [x] `M8.2_RECIPE.md`: Brex adopted as *structural/compositional* foundation (single-accent
  discipline, hairline-border-only elevation, flat surfaces, per-screen component patterns)
  - explicitly not a literal reskin, since this project's already-approved near-black/
  graphite/amber "Ledger Terminal" palette, IBM Plex typography, and sharp 1-2px radius
  system are kept per the milestone's own "color/skin is secondary" instruction.
- [x] Structural rebuild across Dashboard+WalletCard, Transfer, Activity, Reconciliation:
  banded "Portfolio snapshot" stat panel and borderless currency-group cards on Dashboard
  (Brex "Customer Logo Grid" / "Feature Category Card"); Email-Capture-Input field+button
  pairing for the deposit drawer and the Transfer amount+quick-chip row; a 3-tier action
  hierarchy (filled primary / outlined "Clear form" / text-link regenerate) on Transfer's
  confirm node, modeled on Brex's Cookie Consent Dialog button row; Reconciliation collapsed
  from a banner-plus-disconnected-stat-strip into a single instrument-panel block (one
  dominant display-size status read, everything else demoted to a compact secondary meta
  row, methodology text moved to directly caption the table it describes); Activity's own
  already-sound double-entry journal structure was left as-is after honest re-evaluation,
  with two targeted fixes (a nested filled box removed per Brex's "no nested boxes," and
  type/direction badges recolored to neutral per Brex's single-accent Do/Don't - only the
  genuinely-reversed-transaction badge keeps an accent color now).
- [x] Antigravity visual QA (this session, browser-driven against the real localhost): all
  4 screens x 5 breakpoints (320/375/768/1024/1440) x 2 themes = 40 combinations, zero
  console errors, zero horizontal page overflow. One IMPORTANT found and fixed: the
  Transfer source-wallet pill selector clipped its second pill's text mid-word at exactly
  320px width with no visual affordance that it was still scrollable; fixed with a
  right-edge `mask-image` fade (a no-op when the row doesn't overflow) instead of a hard
  clip. Re-verified 0 overflow, fade renders correctly. All 4 Spell adaptations verified
  live: Pop Button's border-width (4px rest -> 2px+scaleY on press) confirmed via
  `getComputedStyle` on both the Dashboard empty-state CTA and the Transfer confirm button;
  Kbd's keycap rendering and symbol mapping confirmed in the command palette; Copy Button's
  icon crossfade confirmed rendering without error.
- [x] Codex QA functional regression: full click-through E2E against the real backend (not
  mocked) - login, second-wallet creation, a same-currency transfer, activity showing the
  posted transfer, a reversal, and reconciliation confirming `Balanced`/zero-drift
  afterward - zero console/network errors throughout. Backend: `dotnet build` clean (0
  warnings), full suite re-run (54 Domain + 90 Application + 58 IntegrationTests via real
  Testcontainers Postgres) all green - confirms the frontend-only diff caused zero backend
  regression.
- [x] Regression gate: frontend `npm run typecheck`/`lint`/`build` all clean after every
  round of changes (only pre-existing `set-state-in-effect`/`only-export-components`
  warnings, none new).
- [x] Security sanity check (frontend-presentation-only diff, not a full SECURITY GATE):
  no `dangerouslySetInnerHTML`/`eval`, no hardcoded secrets, no new runtime network calls
  (the inlined Lucide icon SVGs and Spell source-URL comments are static, not `fetch`
  targets), `.env`/`.env.local` untouched and still gitignored.
- [x] Restored a screen-reader-only `visually-hidden` balanced/drift announcement span in
  `ReconciliationPage.tsx`'s drift table cell that an earlier pass had accidentally dropped
  - caught during this session's evidence-audit review, fixed before the structural rebuild
  began so the rebuild didn't have to carry the regression forward.
- [ ] Deployment - still deliberately not done; local dev server left running for the
  product owner's own visual inspection before any next-step decision.

## M8.3 — Overnight autonomous QA pass (unattended run while the product owner slept)
- [x] Reconstructed real state first (git status/log/diff, MEMORY.md, ROADMAP.md, M8.2
  QA docs) before touching anything: working tree was clean, HEAD already at M8.2's
  commit, no remote configured yet. Confirmed M8.2 was genuinely complete, not repeated.
- [x] Verified the M8.2 QA report's tooling claims before trusting or repeating them,
  since this repo's own M8.2 evidence audit had previously caught an *earlier* session
  fabricating design-sourcing claims: confirmed "Antigravity" = the real Maestri Canvas
  Portal browser tool (not fabricated - its own report byline says so), and confirmed
  Codex CLI (`codex-cli 0.156.1`) is a real, installed, trusted-for-this-project binary.
  Playwright itself is not a project dependency but its browser binaries were already
  cached on the machine (`~/.cache/ms-playwright`), so a real headless visual QA pass was
  possible via a one-off `npm install playwright --no-save` in a scratch directory (never
  added to the project's own `package.json`/lock file).
- [x] Re-ran the full existing gate independently rather than trusting old numbers:
  backend `dotnet build`/`dotnet test` (54 Domain + 90 Application + 58 Integration, all
  green, real Postgres via Testcontainers) and frontend `typecheck`/`lint`/`build`, all
  clean, before making any change.
- [x] Real visual QA: seeded a fresh test user with 4 wallets across 3 currencies (USD
  x2, EUR, GBP) via the live API (funding, a same-currency transfer, and a reversal - not
  mock data), then used a real headless Playwright script against the live
  `localhost:5173` dev server to capture all 40 combinations (4 pages x 5 breakpoints x
  2 themes) with an automated `document.documentElement.scrollWidth` check per
  screenshot. Result: 0 console errors, 0 horizontal overflow, confirmed
  programmatically, not just by eye.
- [x] Two real, verified IMPORTANT findings from actually looking at the screenshots
  (not from re-reading old docs), both fixed and re-verified after a dev-server restart
  (Vite doesn't always serve fresh CSS to a new Playwright context otherwise - see
  MEMORY.md's existing note on this):
  1. **Transfer's composition column was left-pinned, not centered** - at the shell's
     full 1440px width this left roughly two-thirds of the viewport as dead black space,
     undermining the "one of the best screens in the project" bar for a screen that is
     otherwise sound. Fixed with `margin: 0 auto` on `.composition`/`.receiptPanel`/
     `.stateBox` in `TransferPage.module.css` - kept the existing single-column recipe
     (M8.2 had deliberately rejected a competing two-pane workbench; this doesn't
     reopen that decision, it just balances the column that decision produced).
  2. **Mobile responsive-table label/value word-wrap bug**: at <=640px, a table row's
     `data-label` pseudo-element (e.g. "ACCOUNT") and a real English word value (e.g.
     "UserWallet", Reconciliation's account-type text) could both wrap mid-word inside
     the shared flex row, because the row's `overflow-wrap: anywhere` safety net (added
     for long mono IDs) was inherited by short readable words too. Fixed by giving the
     label (`Table.module.css`) and `.accountType` (`ReconciliationPage.module.css`)
     `white-space: nowrap` - the long UUID alongside them still wraps freely, which is
     the safety net's actual intended target.
- [x] Independent functional QA against the real backend (not mocks), covering the
  same checklist the product owner specified: registration, duplicate-email (409),
  wrong-password and unknown-email login (both a generic 401, non-enumerable),
  ownership isolation across two real users (404 on GET/fund/transfer-out/reverse
  targeting another user's wallet - never a 403, matching this repo's documented
  convention), cross-currency transfer rejection (400), idempotency replay (identical
  key+body returns the *same* transaction id, not a duplicate; identical key with a
  *different* body is rejected 409), insufficient-funds rejection (422), and
  reconciliation staying `isClean`/`isBalanced` with zero drift throughout. All passed
  with concrete evidence (exact HTTP status + body), zero regressions found.
- [x] Codex QA: launched non-interactively (`codex exec -s read-only`) with the same
  checklist above handed to it as a genuinely independent second reviewer. It hit its
  account usage limit before returning a result ("You've hit your usage limit... try
  again at Sep 28th, 2026"). Per the product owner's own standing instruction for this
  exact scenario, Claude took over the functional QA pass directly instead of waiting -
  see the independent-QA bullet above, which *is* that fallback pass, not a second
  redundant one.
- [x] Full regression gate re-run one final time after the two fixes: backend 54+90+58
  tests green again, frontend `typecheck`/`lint`/`build` clean (only the same
  pre-existing `set-state-in-effect`/`only-export-components` warnings, no new ones).
- [x] Light security sanity check on tonight's diff (CSS-only, no `dangerouslySetInnerHTML`
  /`eval`/hardcoded secrets); separately, since tonight was also the *first-ever* push of
  this repo's full history to a new public GitHub remote, ran a full `git log --all -p`
  secret-scan grep pass before pushing (see MEMORY.md) - clean, consistent with the M7
  security gate's own prior full-history scan.
- [ ] Deployment - still not done; out of scope for tonight per explicit instruction
  (GitHub push authorized, Neon/Render/Vercel production deployment explicitly not).

## Known environment blockers (see MEMORY.md for detail)
- ~~Docker CLI not available locally~~ — resolved 2026-09-23: Docker Desktop's WSL
  integration works via `docker.exe` from this WSL distro (Docker Desktop just
  needed to be running). `docker compose up -d` + `dotnet ef database update` work
  locally now. First-ever local run of the full test suite against real Postgres:
  54 Domain + 90 Application + 58 IntegrationTests, all green.
