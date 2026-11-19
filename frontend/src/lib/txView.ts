import type { EntryDirection, LedgerEntry, Transaction } from '../api'

export type TxKind = 'deposit' | 'send' | 'receive' | 'reversal'

export interface TxView {
  kind: TxKind
  label: string
  ownDirection: EntryDirection
  ownEntry: LedgerEntry | undefined
  counterEntry: LedgerEntry | undefined
  /** Effect on this wallet's balance: own Debit raises it (+), own Credit lowers it (-). */
  signedMinorUnits: number
}

function leg(tx: Transaction, direction: EntryDirection): LedgerEntry | undefined {
  return tx.entries.find((e) => e.direction === direction)
}

/** Same perspective rule ActivityPage always used: the wallet's own leg Debit = money in. */
export function describeTx(tx: Transaction, walletId: string): TxView {
  const debit = leg(tx, 'Debit')
  const credit = leg(tx, 'Credit')
  const debitIsOwn = debit?.accountId === walletId
  const ownEntry = debitIsOwn ? debit : credit
  const counterEntry = debitIsOwn ? credit : debit
  const ownDirection: EntryDirection = debitIsOwn ? 'Debit' : 'Credit'
  const amount = ownEntry?.amountMinorUnits ?? 0

  let kind: TxKind
  let label: string
  if (tx.type === 'SimulatedFunding') {
    kind = 'deposit'
    label = 'Deposit'
  } else if (tx.type === 'Reversal') {
    kind = 'reversal'
    label = 'Reversal'
  } else if (ownDirection === 'Debit') {
    kind = 'receive'
    label = 'Received'
  } else {
    kind = 'send'
    label = 'Sent'
  }

  return {
    kind,
    label,
    ownDirection,
    ownEntry,
    counterEntry,
    signedMinorUnits: ownDirection === 'Debit' ? amount : -amount,
  }
}

export function shortId(id: string): string {
  return id.length > 14 ? `${id.slice(0, 6)}…${id.slice(-4)}` : id
}
