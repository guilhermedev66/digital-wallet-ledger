import { motion, useReducedMotion } from 'motion/react'
import { useId, useState, type PointerEvent } from 'react'
import type { Currency } from '../api'
import { formatAmount } from '../lib/money'

const W = 640
const H = 200
const PAD_T = 18
const PAD_B = 14

// Smooth curve through the points (cardinal-spline -> cubic Bezier).
function smoothPath(pts: Array<[number, number]>): string {
  if (pts.length === 0) return ''
  let d = `M${pts[0][0]} ${pts[0][1]}`
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] ?? pts[i]
    const p1 = pts[i]
    const p2 = pts[i + 1]
    const p3 = pts[i + 2] ?? p2
    const c1: [number, number] = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6]
    const c2: [number, number] = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6]
    d += ` C${c1[0].toFixed(1)} ${c1[1].toFixed(1)} ${c2[0].toFixed(1)} ${c2[1].toFixed(1)} ${p2[0]} ${p2[1].toFixed(1)}`
  }
  return d
}

/**
 * Wave chart of daily volume moved in ONE currency, on the big violet gradient. The curve
 * is revealed left-to-right by scaling a clip rect (transform only).
 */
export function VolumeWaveCard({
  values,
  labels,
  currency,
  deposited,
  transfersToday,
}: {
  values: number[]
  labels: string[]
  currency: Currency
  deposited: number
  transfersToday: number
}) {
  const reduce = useReducedMotion()
  const uid = useId().replace(/:/g, '')
  const [active, setActive] = useState<number | null>(null)
  const max = Math.max(...values, 1)
  const hasData = values.some((v) => v > 0)
  const innerH = H - PAD_T - PAD_B
  const pts: Array<[number, number]> = values.map((v, i) => [
    (i / Math.max(values.length - 1, 1)) * W,
    PAD_T + (1 - v / max) * innerH,
  ])
  const line = smoothPath(pts)
  const area = `${line} L${W} ${H} L0 ${H} Z`

  function onMove(e: PointerEvent<SVGSVGElement>) {
    const rect = e.currentTarget.getBoundingClientRect()
    const x = ((e.clientX - rect.left) / rect.width) * W
    setActive(Math.max(0, Math.min(values.length - 1, Math.round((x / W) * (values.length - 1)))))
  }

  const a = active !== null ? pts[active] : null

  return (
    <section
      className="relative flex flex-col justify-between gap-4 overflow-hidden rounded-[24px] bg-[linear-gradient(135deg,#2c1fa8_0%,#4a21ef_62%,#6f4bff_100%)] p-6 text-white ring-1 ring-white/20"
      aria-labelledby="wave-title"
    >
      <div className="flex items-baseline justify-between gap-3">
        <h2 id="wave-title" className="text-lg font-bold">
          Volume moved · 30 days
        </h2>
        <span className="rounded-full bg-white/15 px-3 py-1 text-xs font-extrabold ring-1 ring-white/30">{currency}</span>
      </div>

      <div className="relative">
        <svg
          viewBox={`0 0 ${W} ${H}`}
          className="block h-auto w-full touch-none"
          role="img"
          aria-label={
            hasData
              ? `Daily volume moved in ${currency} over the last 30 days, peak ${formatAmount(max, currency)}`
              : `No volume moved in ${currency} over the last 30 days`
          }
          onPointerMove={onMove}
          onPointerLeave={() => setActive(null)}
        >
          <defs>
            <linearGradient id={`${uid}-fill`} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#79efbd" stopOpacity="0.55" />
              <stop offset="100%" stopColor="#79efbd" stopOpacity="0" />
            </linearGradient>
            <linearGradient id={`${uid}-line`} x1="0" y1="0" x2="1" y2="0">
              <stop offset="0%" stopColor="#ffffff" />
              <stop offset="100%" stopColor="#79efbd" />
            </linearGradient>
            <clipPath id={`${uid}-clip`}>
              <motion.rect
                x="0"
                y="0"
                width={W}
                height={H}
                style={{ transformBox: 'view-box', transformOrigin: '0px 0px' }}
                initial={{ scaleX: reduce ? 1 : 0 }}
                animate={{ scaleX: 1 }}
                transition={{ duration: 1.1, ease: [0.22, 1, 0.36, 1], delay: 0.15 }}
              />
            </clipPath>
          </defs>
          {[0.25, 0.5, 0.75].map((f) => (
            <line key={f} x1="0" x2={W} y1={PAD_T + f * innerH} y2={PAD_T + f * innerH} stroke="#fff" strokeOpacity="0.12" strokeDasharray="4 6" />
          ))}
          <g clipPath={`url(#${uid}-clip)`}>
            <path d={area} fill={`url(#${uid}-fill)`} />
            <path d={line} fill="none" stroke={`url(#${uid}-line)`} strokeWidth="3.5" strokeLinecap="round" strokeLinejoin="round" />
          </g>
          {a && (
            <g>
              <line x1={a[0]} x2={a[0]} y1="0" y2={H} stroke="#fff" strokeOpacity="0.35" />
              <circle cx={a[0]} cy={a[1]} r="6" fill="#79efbd" stroke="#fff" strokeWidth="2" />
            </g>
          )}
        </svg>
        {a && active !== null && (
          <div
            className="pointer-events-none absolute top-0 -translate-x-1/2 rounded-xl bg-[#0d0b33]/90 px-3 py-1.5 text-xs font-semibold shadow-none ring-1 ring-white/25"
            style={{ left: `${(a[0] / W) * 100}%` }}
            role="status"
          >
            <span className="text-white/70">{labels[active]}</span>{' '}
            <span className="amount font-bold">{formatAmount(values[active], currency)}</span>
          </div>
        )}
        {!hasData && (
          <p className="absolute inset-0 grid place-items-center text-sm font-semibold text-white/80">
            No movement in this currency yet
          </p>
        )}
        <div className="mt-1 flex justify-between text-[11px] font-semibold text-white/70">
          <span>{labels[0]}</span>
          <span>{labels[Math.floor(labels.length / 2)]}</span>
          <span>{labels[labels.length - 1]}</span>
        </div>
      </div>

      <dl className="m-0 grid grid-cols-2 gap-4 border-t border-white/20 pt-4">
        <div>
          <dt className="text-xs font-semibold text-white/75">Total deposited · 30d</dt>
          <dd className="amount m-0 mt-1 text-2xl font-extrabold tracking-tight">{formatAmount(deposited, currency)}</dd>
        </div>
        <div>
          <dt className="text-xs font-semibold text-white/75">Transfers today</dt>
          <dd className="amount m-0 mt-1 text-2xl font-extrabold tracking-tight">{transfersToday}</dd>
        </div>
      </dl>
    </section>
  )
}
