import { motion, useReducedMotion } from 'motion/react'

/**
 * Balance scale: left pan = Σ Debits, right pan = Σ Credits. `tilt` is in degrees
 * (0 = level, positive = right/credits side lower). It starts unlevelled and settles
 * with a spring. Pans counter-rotate so they hang level from the beam.
 */
export function LedgerScale({
  tilt,
  leftValue,
  rightValue,
  caption,
  ariaLabel,
}: {
  tilt: number
  leftValue?: string
  rightValue?: string
  caption: string
  ariaLabel: string
}) {
  const reduce = useReducedMotion()
  const start = reduce ? tilt : tilt === 0 ? -14 : -Math.sign(tilt) * 14
  const spring = reduce
    ? { duration: 0 }
    : ({ type: 'spring', stiffness: 70, damping: 7, mass: 1.1, delay: 0.35 } as const)
  const settled = tilt === 0
  const tone = settled ? 'var(--color-credit)' : 'var(--color-danger)'
  const pivot = { transformBox: 'view-box', transformOrigin: '0px 0px' } as const

  return (
    <figure className="m-0 flex flex-col items-center gap-2">
      <svg viewBox="0 0 320 200" width="100%" style={{ maxWidth: 360 }} role="img" aria-label={ariaLabel}>
        {/* base + pillar */}
        <path d="M130 188h60l-8-16h-44z" fill="var(--color-border-strong)" />
        <rect x="157" y="48" width="6" height="126" rx="3" fill="var(--color-border-strong)" />
        <g transform="translate(160 48)">
          <motion.g style={pivot} initial={{ rotate: start }} animate={{ rotate: tilt }} transition={spring}>
            <rect x="-124" y="-3" width="248" height="6" rx="3" fill="var(--color-text)" />
            {[-1, 1].map((side) => (
              <g key={side} transform={`translate(${side * 120} 0)`}>
                <motion.g style={pivot} initial={{ rotate: -start }} animate={{ rotate: -tilt }} transition={spring}>
                  <path d="M0 0 L-34 62 M0 0 L34 62" stroke="var(--color-text-muted)" strokeWidth="1.5" />
                  <path
                    d="M-40 62h80c0 20-18 30-40 30s-40-10-40-30z"
                    fill={side === -1 ? 'var(--color-surface-elevated)' : 'var(--color-credit-bg)'}
                    stroke={side === -1 ? 'var(--color-text-muted)' : 'var(--color-credit)'}
                    strokeWidth="2"
                  />
                  <text x="0" y="82" textAnchor="middle" fontSize="15" fontWeight="800" fill="var(--color-text)">
                    {side === -1 ? 'Σ D' : 'Σ C'}
                  </text>
                </motion.g>
              </g>
            ))}
            <circle cx="0" cy="0" r="7" fill={tone} />
          </motion.g>
        </g>
      </svg>
      <div className="flex w-full max-w-[360px] justify-between text-xs font-bold text-[var(--color-text-muted)]">
        <span className="amount">Σ Debits{leftValue ? ` · ${leftValue}` : ''}</span>
        <span className="amount">Σ Credits{rightValue ? ` · ${rightValue}` : ''}</span>
      </div>
      <figcaption className="text-center text-sm font-semibold" style={{ color: tone }}>
        {caption}
      </figcaption>
    </figure>
  )
}
