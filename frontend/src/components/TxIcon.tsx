import { ArrowDownLeft, ArrowUpRight, Plus, Undo2 } from 'lucide-react'
import type { TxKind } from '../lib/txView'
import styles from './TxIcon.module.css'

const ICONS = { deposit: Plus, send: ArrowUpRight, receive: ArrowDownLeft, reversal: Undo2 }

export function TxIcon({ kind }: { kind: TxKind }) {
  const Icon = ICONS[kind]
  return (
    <span className={[styles.icon, styles[kind]].join(' ')} aria-hidden="true">
      <Icon size={18} />
    </span>
  )
}
