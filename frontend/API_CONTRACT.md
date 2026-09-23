# Frontend's API contract

This documents the REAL backend contract (M1-M3, committed) — verified directly
against `WalletsController.cs`, `AuthController.cs`, and the `Dtos/` records, not
assumed. An earlier version of this file was written before the backend existed
and guessed a different shape (global `/api/transfers`, cursor pagination, a
`status`/`memo`/`reversalOfTransactionId` on transactions, idempotency key in the
body, 403 for "not yours"). That guess was wrong on every one of those points and
has been corrected here; `src/api/mockClient.ts` and `src/api/httpClient.ts` now
both implement exactly this contract, so swapping one for the other is a true
drop-in (see `src/api/index.ts`).

All money amounts are integer minor units (cents), never floats, per
`../ARCHITECTURE.md`. Auth is a bearer JWT in `Authorization: Bearer <token>`.
Any endpoint that posts a transaction takes the idempotency key as an
`Idempotency-Key` request **header**, never a body field.

```
POST /api/auth/register        { email, password }                 -> 201 { userId }
POST /api/auth/login           { email, password }                 -> 200 { userId, email, accessToken, expiresAtUtc }

GET  /api/wallets                                                   -> WalletDto[]
POST /api/wallets              { currency, displayName? }           -> WalletDto
GET  /api/wallets/:id                                                -> WalletDto | 404

GET  /api/wallets/:id/balance                                        -> WalletBalanceDto | 404

POST /api/wallets/:id/simulate-funding
  headers: Idempotency-Key
  body:    { amountMinorUnits }                                     -> TransactionDto | 400 | 404 | 409

POST /api/wallets/:id/transfer      (:id is the SOURCE wallet)
  headers: Idempotency-Key
  body:    { destinationWalletId, amountMinorUnits }                -> TransactionDto | 400 | 404 | 409 | 422

GET  /api/wallets/:id/history?page=&pageSize=&fromUtc=&toUtc=&type=  -> PagedResult<TransactionDto> | 404
  (fromUtc/toUtc/type are optional filters added in M4; type is "Transfer" |
  "SimulatedFunding" | "Reversal")

POST /api/wallets/:id/transactions/:transactionId/reverse   (M4 - not yet used by the M5 UI)
  headers: Idempotency-Key
  body:    (none)                                                    -> TransactionDto | 400 | 404 | 409 | 422
  Self-service (caller's own JWT) only works when the reversal debits the
  CALLER's own wallet (e.g. voluntarily returning a transfer you received, or
  undoing your own SimulatedFunding) - reversing a transaction the other way
  (clawing funds back out of someone else's wallet) needs the admin role and
  gets the same 404 as any other "not yours" case otherwise. See MEMORY.md
  "Reversal authorization".

GET  /api/wallets/:id/reconciliation    (M4 - not yet used by the M5 UI)   -> ReconciliationReportDto | 404
GET  /api/reconciliation                (M4, admin-only, 404 for non-admin) -> ReconciliationReportDto | 404
```

There is no `POST /api/transfers`, no cursor-based pagination, and no
`GET /api/transactions/:id` — none of those exist on the backend.

```ts
WalletDto {
  id: string
  ownerUserId: string
  currency: "USD" | "BRL" | "EUR" | "GBP" | "CHF" | "CAD" | "AUD"
  displayName: string | null
  createdAtUtc: string
}
// No balance field - a wallet never carries a stored balance (the entire point of
// the project). The frontend composes WalletWithBalance client-side by calling
// GET /api/wallets/:id/balance per wallet - see src/hooks/useWallets.ts.

WalletBalanceDto {
  walletId: string
  currency: "USD" | "BRL" | "EUR" | "GBP" | "CHF" | "CAD" | "AUD"
  balanceMinorUnits: number
}

TransactionDto {
  id: string
  type: "Transfer" | "SimulatedFunding" | "Reversal"
  postedAtUtc: string
  reversalOfTransactionId: string | null   // set only on a Reversal (M4)
  entries: LedgerEntryDto[]
}
// No idempotencyKey, status, or memo in the response. Posting is atomic/synchronous
// (see ARCHITECTURE.md) - a 200 response is already posted, so there is no
// server-side "Pending" status to model client-side either.

LedgerEntryDto {
  accountId: string
  direction: "Debit" | "Credit"
  amountMinorUnits: number
  currency: "USD" | "BRL" | "EUR" | "GBP" | "CHF" | "CAD" | "AUD"
}

PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
}

// M4, not yet used by the M5 UI (reconciliation view is M6 scope):
AccountReconciliationDto {
  accountId: string
  accountType: string
  currency: "USD" | "BRL" | "EUR" | "GBP" | "CHF" | "CAD" | "AUD"
  projectedBalanceMinorUnits: number
  recomputedBalanceMinorUnits: number
  driftMinorUnits: number
  isBalanced: boolean
}
UnbalancedTransactionDto {
  transactionId: string
  currency: "USD" | "BRL" | "EUR" | "GBP" | "CHF" | "CAD" | "AUD"
  totalDebitMinorUnits: number
  totalCreditMinorUnits: number
}
ReconciliationReportDto {
  generatedAtUtc: string
  accounts: AccountReconciliationDto[]
  unbalancedTransactions: UnbalancedTransactionDto[]
  isClean: boolean
}
```

## Conventions carried over from MEMORY.md (backend, verified in code)

- **Never a 403.** Every wallet-scoped endpoint resolves ownership from the JWT
  `sub` claim only; "doesn't exist" and "not yours" are both a 404, so a non-owner
  can't distinguish them. The frontend must not assume 403 means "not yours" -
  that status code should never appear from this API.
- **Idempotency replay.** Retrying the same `(user, Idempotency-Key)` with the
  same parameters returns the original transaction; retrying with different
  parameters is a 409 conflict.
- **Insufficient funds** is a 422, not a 400.

## Known gap the frontend still assumes an answer for

- **Recipient identification for transfers.** The backend takes a raw
  `destinationWalletId` GUID with no lookup/search endpoint. The UI has the user
  paste a wallet ID directly. Revisit if a wallet/user search endpoint is ever
  added.
