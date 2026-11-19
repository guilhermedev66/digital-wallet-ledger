import { motion, useReducedMotion } from 'motion/react'
import type { WalletSummary } from '../hooks/useWallets'
import { CURRENCIES } from '../lib/currency'

/** Physical-card face (credit-card proportions). Presentational only. */
export function WalletCreditCard({
  wallet,
  compact = false,
}: {
  wallet: Pick<WalletSummary, 'id' | 'currency' | 'displayName'>
  compact?: boolean
}) {
  const reduce = useReducedMotion()
  const last4 = wallet.id.replace(/[^a-z0-9]/gi, '').slice(-4).toUpperCase()
  const label = CURRENCIES.find((c) => c.code === wallet.currency)?.label ?? wallet.currency

  return (
    <div className="relative size-full overflow-hidden rounded-[28px] bg-[linear-gradient(135deg,#0d0b33_0%,#2c1fa8_34%,#4a21ef_62%,#79efbd_135%)] p-[6%] text-white ring-1 ring-white/25" style={compact ? { containerType: 'inline-size' } : undefined}>
      {/* subtle pattern: concentric arcs + fine dots */}
      <svg className="pointer-events-none absolute inset-0 size-full opacity-[0.22]" viewBox="0 0 400 252" preserveAspectRatio="xMidYMid slice" aria-hidden="true">
        <defs>
          <pattern id={`dots-${wallet.id}`} width="14" height="14" patternUnits="userSpaceOnUse">
            <circle cx="2" cy="2" r="1" fill="#fff" />
          </pattern>
        </defs>
        <rect width="400" height="252" fill={`url(#dots-${wallet.id})`} opacity="0.5" />
        {[120, 170, 220, 270].map((r) => (
          <circle key={r} cx="360" cy="30" r={r} fill="none" stroke="#fff" strokeWidth="1" />
        ))}
      </svg>
      {/* periodic light sheen */}
      {!reduce && (
        <motion.span
          className="pointer-events-none absolute inset-y-0 w-1/3 -skew-x-[20deg] bg-gradient-to-r from-transparent via-white/25 to-transparent"
          initial={{ left: '-40%' }}
          animate={{ left: ['-40%', '140%'] }}
          transition={{ duration: 2.2, ease: 'easeInOut', repeat: Infinity, repeatDelay: 5 }}
          aria-hidden="true"
        />
      )}

      <div className="relative flex size-full flex-col justify-between">
        <div className="flex items-start justify-between">
          <span className={compact ? 'text-[8px] font-extrabold tracking-[0.16em] text-white/80' : 'text-[11px] font-extrabold tracking-[0.18em] text-white/80'}>WALLET &amp; LEDGER</span>
          <span className={compact ? 'rounded-full bg-white/15 px-2 py-0.5 text-[10px] font-extrabold ring-1 ring-white/30' : 'rounded-full bg-white/15 px-3 py-1 text-sm font-extrabold tracking-wide ring-1 ring-white/30'}>
            {wallet.currency}
          </span>
        </div>

        {/* chip */}
        <span className="relative block h-[15%] w-[17%] min-h-5 min-w-8 overflow-hidden rounded-md bg-gradient-to-br from-[#e9e3ff] via-[#b8a9ff] to-[#79efbd] ring-1 ring-white/50" aria-hidden="true">
          <span className="absolute inset-x-0 top-1/2 h-px bg-[#0d0b33]/35" />
          <span className="absolute inset-y-0 left-1/3 w-px bg-[#0d0b33]/35" />
          <span className="absolute inset-y-0 left-2/3 w-px bg-[#0d0b33]/35" />
        </span>

        <div className="flex items-end justify-between gap-3">
          <div className="min-w-0">
            <p className={compact ? 'truncate text-xs font-bold' : 'truncate text-base font-bold'}>{wallet.displayName ?? 'Untitled wallet'}</p>
            <p className={compact ? 'font-mono text-[10px] tracking-[0.2em] text-white/80' : 'font-mono text-sm tracking-[0.22em] text-white/80'}>•••• {last4}</p>
          </div>
          {!compact && <p className="shrink-0 text-xs font-semibold text-white/75">{label}</p>}
        </div>
      </div>
    </div>
  )
}
