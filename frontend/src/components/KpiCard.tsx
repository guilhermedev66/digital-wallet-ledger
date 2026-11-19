import type { LucideIcon } from 'lucide-react'
import { TrendingDown, TrendingUp, Minus } from 'lucide-react'
import { CountUp } from './CountUp'

type Tone = 'mint' | 'violet' | 'teal' | 'amber'

// Strip colour + the ink that sits on it (all >= 9:1 contrast, identical in both themes).
const TONES: Record<Tone, { bg: string; ink: string }> = {
  mint: { bg: 'var(--strip-mint)', ink: 'var(--strip-ink-dark)' },
  violet: { bg: 'var(--strip-violet)', ink: 'var(--strip-ink-light)' },
  teal: { bg: 'var(--strip-teal)', ink: 'var(--strip-ink-dark)' },
  amber: { bg: 'var(--strip-amber)', ink: 'var(--strip-ink-dark)' },
}

/** KPI: big counted number, label, coloured icon, and a coloured strip with the real trend. */
export function KpiCard({
  label,
  value,
  Icon,
  tone,
  trend,
  direction,
}: {
  label: string
  value: number | null
  Icon: LucideIcon
  tone: Tone
  trend: string
  direction: 'up' | 'down' | 'flat'
}) {
  const { bg, ink } = TONES[tone]
  const Trend = direction === 'up' ? TrendingUp : direction === 'down' ? TrendingDown : Minus
  return (
    <article className="glass group flex flex-col overflow-hidden rounded-[24px] transition-transform duration-200 ease-out hover:-translate-y-1">
      <div className="flex items-start justify-between gap-3 p-5 pb-4">
        <div className="min-w-0">
          <p className="text-[40px] font-extrabold leading-none tracking-tight text-[var(--color-text)]">
            {value === null ? <span className="inline-block h-10 w-16 animate-pulse rounded-lg bg-[var(--color-surface-elevated)]" aria-label="Loading" /> : <CountUp value={value} />}
          </p>
          <p className="mt-2 text-sm font-semibold text-[var(--color-text-muted)]">{label}</p>
        </div>
        <span
          className="grid size-11 shrink-0 place-items-center rounded-full transition-transform duration-200 ease-out group-hover:scale-110"
          style={{ background: bg, color: ink }}
          aria-hidden="true"
        >
          <Icon size={20} />
        </span>
      </div>
      <div
        className="flex items-center gap-2 px-5 py-2.5 text-xs font-bold"
        style={{ background: bg, color: ink }}
      >
        <Trend size={14} aria-hidden="true" />
        <span>{trend}</span>
      </div>
    </article>
  )
}
