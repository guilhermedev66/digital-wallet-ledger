import { motion, useReducedMotion } from 'motion/react'
import { useState, type PointerEvent } from 'react'

export interface TypeCounts {
  deposit: number
  transfer: number
  reversal: number
}

const SEGMENTS = [
  { key: 'deposit', label: 'Deposits', color: 'var(--color-credit)' },
  { key: 'transfer', label: 'Transfers', color: 'var(--chart-violet)' },
  { key: 'reversal', label: 'Reversals', color: 'var(--color-pending)' },
] as const

const R = 62
const STROKE = 26
const C = 2 * Math.PI * R
const GAP = 4

/** Donut of transaction COUNTS by type (no amounts, so no currency mixing). Spins and fills in. */
export function TypeDonutCard({ counts }: { counts: TypeCounts }) {
  const reduce = useReducedMotion()
  const [hover, setHover] = useState<{ index: number; x: number; y: number } | null>(null)
  const total = counts.deposit + counts.transfer + counts.reversal
  const pct = (n: number) => (total === 0 ? 0 : Math.round((n / total) * 100))

  let offset = 0
  const arcs = SEGMENTS.map((s, index) => {
    const count = counts[s.key]
    const len = total === 0 ? 0 : (count / total) * C
    const arc = { ...s, index, count, len: Math.max(len - (count > 0 && total > count ? GAP : 0), 0), start: offset }
    offset += len
    return arc
  })

  function move(index: number, e: PointerEvent<SVGCircleElement>) {
    const box = e.currentTarget.ownerSVGElement?.parentElement?.getBoundingClientRect()
    if (!box) return
    setHover({ index, x: e.clientX - box.left, y: e.clientY - box.top })
  }

  const hovered = hover ? arcs[hover.index] : null

  return (
    <section className="glass flex flex-col gap-4 rounded-[24px] p-6" aria-labelledby="donut-title">
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="donut-title" className="text-lg font-bold">
          Transactions by type
        </h2>
        <span className="text-xs font-semibold text-[var(--color-text-muted)]">last 30 days</span>
      </div>

      <div className="flex flex-col items-center gap-6 sm:flex-row sm:justify-around">
        <div className="relative size-[190px] shrink-0">
          <motion.svg
            viewBox="0 0 160 160"
            className="size-full"
            role="img"
            aria-label={
              total === 0
                ? 'No transactions in the last 30 days'
                : `Transactions by type: ${arcs.map((a) => `${a.count} ${a.label.toLowerCase()} (${pct(a.count)}%)`).join(', ')}`
            }
            initial={reduce ? { opacity: 0 } : { rotate: -140, opacity: 0, scale: 0.8 }}
            animate={{ rotate: 0, opacity: 1, scale: 1 }}
            transition={reduce ? { duration: 0.25 } : { type: 'spring', stiffness: 60, damping: 14, mass: 1.2 }}
          >
            <g transform="rotate(-90 80 80)">
              <circle cx="80" cy="80" r={R} fill="none" stroke="var(--color-surface-elevated)" strokeWidth={STROKE} />
              {arcs.map((a) =>
                a.count > 0 ? (
                  <g key={a.key} transform={`rotate(${(a.start / C) * 360} 80 80)`}>
                    <motion.circle
                      cx="80"
                      cy="80"
                      r={R}
                      fill="none"
                      stroke={a.color}
                      strokeWidth={hover?.index === a.index ? STROKE + 4 : STROKE}
                      tabIndex={0}
                      aria-label={`${a.label}: ${a.count} (${pct(a.count)}%)`}
                      style={{ cursor: 'pointer', outline: 'none' }}
                      initial={reduce ? false : { pathLength: 0 }}
                      animate={{ pathLength: a.len / C }}
                      transition={{ duration: 0.9, delay: 0.2 + a.index * 0.12, ease: 'easeOut' }}
                      onPointerMove={(e) => move(a.index, e)}
                      onPointerLeave={() => setHover(null)}
                      onFocus={() => setHover({ index: a.index, x: 95, y: 20 })}
                      onBlur={() => setHover(null)}
                    />
                  </g>
                ) : null,
              )}
            </g>
          </motion.svg>
          <div className="pointer-events-none absolute inset-0 grid place-items-center text-center">
            <div>
              <p className="amount m-0 text-3xl font-extrabold leading-none">{hovered ? hovered.count : total}</p>
              <p className="m-0 mt-1 text-xs font-semibold text-[var(--color-text-muted)]">{hovered ? hovered.label : 'transactions'}</p>
            </div>
          </div>
          {hover && hovered && (
            <div
              role="status"
              className="pointer-events-none absolute z-10 -translate-x-1/2 -translate-y-full whitespace-nowrap rounded-xl bg-[var(--color-surface-elevated)] px-3 py-1.5 text-xs font-bold ring-1 ring-[var(--color-border-strong)]"
              style={{ left: hover.x, top: hover.y - 8 }}
            >
              <span style={{ color: hovered.color }}>●</span> {hovered.label}: {hovered.count} · {pct(hovered.count)}%
            </div>
          )}
        </div>

        <ul className="m-0 flex w-full max-w-[240px] list-none flex-col gap-3 p-0">
          {arcs.map((a) => (
            <li key={a.key} className="flex items-center gap-3 text-sm">
              <span className="size-3 shrink-0 rounded-full" style={{ background: a.color }} aria-hidden="true" />
              <span className="flex-1 font-semibold">{a.label}</span>
              <span className="amount font-bold">{a.count}</span>
              <span className="amount w-10 text-right text-[var(--color-text-muted)]">{pct(a.count)}%</span>
            </li>
          ))}
        </ul>
      </div>
    </section>
  )
}
