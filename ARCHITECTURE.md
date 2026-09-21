# Architecture & Financial Invariants

## Shape

Modular monolith, not microservices. One deployable API, clear internal boundaries:

```
src/
  WalletLedger.Domain          # entities, value objects, invariants. No EF, no framework deps.
  WalletLedger.Application     # use cases (commands/queries), interfaces (ports), DTOs.
  WalletLedger.Infrastructure  # EF Core, PostgreSQL, repository implementations, migrations.
  WalletLedger.Api             # ASP.NET Core: controllers, auth, DI wiring, middleware.
tests/
  WalletLedger.Domain.Tests        # pure unit tests of invariants.
  WalletLedger.Application.Tests   # use case tests, mocked ports.
  WalletLedger.IntegrationTests    # real PostgreSQL (Testcontainers), concurrency tests.
frontend/                     # React + TypeScript + Vite, consumes the API only.
```

Dependency direction: `Api -> Infrastructure -> Application -> Domain`. Domain depends on nothing.

## Why not Repository-per-aggregate with generic interfaces

Avoided a generic `IRepository<T>` abstraction. The ledger's correctness depends on
specific, intention-revealing operations (`PostTransactionAsync`, `GetBalanceAsOf`,
not generic `Add`/`Update`) so that atomicity and invariant-checking live next to the
operation, not scattered in callers. Revisit only if a second persistence backend is
ever genuinely needed (it won't be for this project).

## The ledger is the source of truth

There is no mutable `Wallet.Balance` column. A wallet/account's balance is **derived**
by summing its posted ledger entries. This is the entire point of the project.

### Core model

- `LedgerAccount` — one per wallet (and one per internal system account, e.g. a
  "simulated funding source" account used for demo deposits). Has a `Currency`.
- `Transaction` (aka journal entry) — an immutable, atomically-posted group of
  `LedgerEntry` rows. Has an `Id`, `IdempotencyKey`, `PostedAtUtc`, `Type`
  (Transfer, SimulatedFunding, Reversal), optional `ReversalOfTransactionId`.
- `LedgerEntry` — immutable row: `TransactionId`, `AccountId`, `Direction`
  (Debit/Credit), `AmountMinorUnits` (positive `long`), `Currency`. Never updated or
  deleted after insert.

### Invariant: a transaction balances

For every `Transaction`, grouped by currency:

```
SUM(entries where Direction = Debit).AmountMinorUnits
  == SUM(entries where Direction = Credit).AmountMinorUnits
```

Enforced in the Domain layer before a transaction can be constructed (constructor/
factory throws if unbalanced — it must be structurally impossible to persist an
unbalanced transaction), and re-verified by the reconciliation job as a defense in
depth check against bugs or manual DB tampering.

### Money

- `AmountMinorUnits: long` — integer minor units (cents). **Never** `float`/`double`
  for money, anywhere, including DTOs and JSON contracts (serialize as integer, or
  as a string if a future decimal-currency need arises — decide per currency, not
  globally).
- `Currency` — explicit value object/enum (starting with `USD`/`BRL` demo
  currencies). No implicit cross-currency arithmetic: a transaction's entries must
  all share one currency; a "transfer" between wallets of different currencies is
  out of scope unless a later milestone adds an explicit FX conversion step with
  its own documented rounding rule.
- Rounding: not needed for integer minor units on same-currency transfers. If FX or
  fee splitting is added later, the rounding rule must be written down at that time,
  before implementation.

### Balances

`GetBalance(accountId)` sums `LedgerEntry` for that account (credits − debits, sign
per account type) as of "now" or as of a point in time for historical queries. This
can be optimized later with a materialized/cached projection **as long as**:
1. the projection is rebuildable from the ledger alone, and
2. reconciliation compares the projection against a fresh ledger sum and flags drift.

The projection is a cache, never the authority.

### Atomicity, idempotency, concurrency

- **Atomicity**: a transaction and all its entries are posted inside a single
  database transaction (`SERIALIZABLE` or `REPEATABLE READ` as needed for the
  posting path). Either every entry is inserted or none are.
- **Idempotency**: every transfer/funding command carries a client-supplied
  `IdempotencyKey`. The `Transaction` table has a unique constraint on
  `(RequestedByUserId, IdempotencyKey)`. A retried command with the same key returns
  the original result instead of double-posting.
- **Concurrency**: two concurrent transfers debiting the same account must not both
  succeed past the available balance. Use a DB-enforced check (e.g. row lock via
  `SELECT ... FOR UPDATE` on the source account during posting, or an
  application-level serializable retry loop) — not an in-memory check-then-act,
  which races. Covered by real integration tests that fire concurrent requests
  against real PostgreSQL, not mocks.
- **Insufficient funds**: checked against the authoritative balance inside the same
  atomic operation that posts the debit, not in a separate earlier read.

### Reversals

Posted entries are never edited or deleted. A reversal is a new `Transaction` with
`Type = Reversal` and `ReversalOfTransactionId` set, whose entries are the mirror
image of the original. History always shows both the original and the reversal.

### Reconciliation

A reconciliation routine (endpoint + can be scheduled) that, for a given account or
globally:
- recomputes every account balance from raw `LedgerEntry` rows,
- verifies every `Transaction`'s entries balance per currency,
- reports any drift between a cached balance projection and the recomputed value.

This is a first-class feature, not an afterthought — it is the project's proof that
the ledger is trustworthy.

## AuthN/AuthZ

- JWT-based auth. Every wallet/ledger endpoint requires the caller to be
  authenticated **and** resolves ownership server-side from the authenticated
  user's claims — never from a client-supplied `userId`/`ownerId` in the request
  body or query string.
- Ownership check: `walletId` in the route must belong to the authenticated user
  (or the caller must hold an explicit admin role), checked in the Application
  layer before any read or write, not just hidden in the UI.

## What this project deliberately does not do

- No real payment processors, no real bank connectivity, no real money movement.
- No microservices, no message broker, no Kubernetes — a modular monolith is the
  right size for this scope and keeps the ledger's transactional boundary simple
  (single DB transaction, no distributed saga needed).
