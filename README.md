# Digital Wallet & Ledger

Portfolio-grade fintech engineering project simulating a digital wallet / financial
ledger system. It does not process real money and does not connect to real banks.

The engineering centerpiece is a double-entry ledger: immutable posted entries,
balances derived from the ledger (never a mutable column), atomic transfers,
idempotency, concurrency correctness, and reconciliation. See `ARCHITECTURE.md`
for the full model and rationale, and `ROADMAP.md` for milestone status.

## Stack

- Backend: C# / ASP.NET Core, EF Core, PostgreSQL
- Frontend: React, TypeScript, Vite
- Infra: Docker Compose (Postgres), GitHub Actions CI

## Getting started

```bash
# Backend
dotnet restore WalletLedger.slnx
dotnet build WalletLedger.slnx
dotnet test WalletLedger.slnx

# Database (requires Docker)
docker compose up -d

# Frontend
cd frontend
npm install
npm run dev
```

Copy `.env.example` to `.env` and adjust values for local development. Never commit
`.env`.
