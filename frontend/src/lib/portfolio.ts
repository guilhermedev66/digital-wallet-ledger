import type { Currency, Transaction } from '../api'

const DAY = 24 * 60 * 60 * 1000

export function startOfToday(now = new Date()): number {
  return new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime()
}

/** A transaction's headline amount: its Debit leg (both legs always match). */
export function txAmount(tx: Transaction): number {
  return tx.entries.find((e) => e.direction === 'Debit')?.amountMinorUnits ?? 0
}

export function txCurrency(tx: Transaction): Currency | null {
  return tx.entries[0]?.currency ?? null
}

export interface TypeWindow {
  today: number
  week: number
  prevWeek: number
}

/** Counts only (never amounts), so mixing wallets/currencies is safe. */
export function countWindows(txs: Transaction[], type: Transaction['type'], now = new Date()): TypeWindow {
  const t = now.getTime()
  const today = startOfToday(now)
  const out: TypeWindow = { today: 0, week: 0, prevWeek: 0 }
  for (const tx of txs) {
    if (tx.type !== type) continue
    const at = Date.parse(tx.postedAtUtc)
    if (at >= today) out.today++
    if (at >= t - 7 * DAY) out.week++
    else if (at >= t - 14 * DAY) out.prevWeek++
  }
  return out
}

/** Daily volume moved (sum of transaction amounts) for ONE currency, oldest -> newest. */
export function volumeSeries(txs: Transaction[], currency: Currency, days = 30, now = new Date()) {
  const start = startOfToday(now) - (days - 1) * DAY
  const values = Array<number>(days).fill(0)
  for (const tx of txs) {
    if (txCurrency(tx) !== currency) continue
    const idx = Math.floor((Date.parse(tx.postedAtUtc) - start) / DAY)
    if (idx >= 0 && idx < days) values[idx] += txAmount(tx)
  }
  const fmt = new Intl.DateTimeFormat('en-US', { month: 'short', day: 'numeric' })
  const labels = values.map((_, i) => fmt.format(start + i * DAY))
  return { values, labels }
}

/** Total deposited in ONE currency over the last `days` days. */
export function depositedTotal(txs: Transaction[], currency: Currency, days = 30, now = new Date()): number {
  const since = startOfToday(now) - (days - 1) * DAY
  return txs
    .filter((tx) => tx.type === 'SimulatedFunding' && txCurrency(tx) === currency && Date.parse(tx.postedAtUtc) >= since)
    .reduce((n, tx) => n + txAmount(tx), 0)
}

export function transfersTodayCount(txs: Transaction[], currency: Currency, now = new Date()): number {
  const today = startOfToday(now)
  return txs.filter((tx) => tx.type === 'Transfer' && txCurrency(tx) === currency && Date.parse(tx.postedAtUtc) >= today).length
}

export function typeCounts(txs: Transaction[]) {
  const c = { deposit: 0, transfer: 0, reversal: 0 }
  for (const tx of txs) {
    if (tx.type === 'SimulatedFunding') c.deposit++
    else if (tx.type === 'Transfer') c.transfer++
    else c.reversal++
  }
  return c
}
