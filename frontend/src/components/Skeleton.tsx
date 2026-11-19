import type { CSSProperties } from 'react'
import styles from './Skeleton.module.css'

/** One shimmering placeholder block (the sweep animates transform only). */
export function Skeleton({
  width = '100%',
  height,
  radius,
  className,
}: {
  width?: string | number
  height?: string | number
  radius?: string | number
  className?: string
}) {
  const style: CSSProperties = { width, height: height ?? '1em', borderRadius: radius }
  return <span className={[styles.block, className].filter(Boolean).join(' ')} style={style} aria-hidden="true" />
}

function Status({ children, label = 'Loading' }: { children: React.ReactNode; label?: string }) {
  return (
    <div role="status" aria-label={label} className={styles.status}>
      {children}
      <span className="visually-hidden">{label}…</span>
    </div>
  )
}

export function SkeletonRows({ count, width }: { count: number; width?: string | number }) {
  return (
    <Status>
      {Array.from({ length: count }).map((_, i) => (
        <Skeleton key={i} width={width} />
      ))}
    </Status>
  )
}

/** Credit-card-shaped placeholder (matches the Dashboard wallet card). */
export function SkeletonCard({ label = 'Loading wallet' }: { label?: string }) {
  return (
    <Status label={label}>
      <div className={styles.cardWrap}>
        <Skeleton width="100%" height="100%" radius={28} className={styles.card} />
      </div>
      <div className={styles.centered}>
        <Skeleton width={120} height={14} />
        <Skeleton width="min(420px, 80%)" height={64} radius={16} />
      </div>
    </Status>
  )
}

/** Transaction-list-shaped placeholder: round icon, two text lines, amount. */
export function SkeletonList({ rows = 4, label = 'Loading transactions' }: { rows?: number; label?: string }) {
  return (
    <Status label={label}>
      {Array.from({ length: rows }).map((_, i) => (
        <div key={i} className={styles.listRow}>
          <Skeleton width={40} height={40} radius="50%" />
          <div className={styles.listText}>
            <Skeleton width="38%" height={14} />
            <Skeleton width="62%" height={12} />
          </div>
          <Skeleton width={72} height={16} />
        </div>
      ))}
    </Status>
  )
}

/** Table-shaped placeholder: header bar and rows of cells. */
export function SkeletonTable({ rows = 3, cols = 5, label = 'Loading table' }: { rows?: number; cols?: number; label?: string }) {
  return (
    <Status label={label}>
      <div className={styles.tableRow}>
        {Array.from({ length: cols }).map((_, c) => (
          <Skeleton key={c} height={12} width="60%" />
        ))}
      </div>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className={styles.tableRow}>
          {Array.from({ length: cols }).map((_, c) => (
            <Skeleton key={c} height={16} width={c === 0 ? '80%' : '55%'} />
          ))}
        </div>
      ))}
    </Status>
  )
}

/** Scoreboard-shaped placeholder: big number, dot grid, metric rows. */
export function SkeletonScore() {
  return (
    <Status label="Recomputing ledger balances from raw entries">
      <div className={styles.score}>
        <div className={styles.scoreLeft}>
          <Skeleton width={90} height={12} />
          <Skeleton width={110} height={76} radius={18} />
          <Skeleton width={170} height={64} radius={12} />
        </div>
        <div className={styles.scoreRight}>
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className={styles.listRow}>
              <Skeleton width={36} height={36} radius="50%" />
              <Skeleton width="45%" height={12} />
              <Skeleton width={60} height={16} />
            </div>
          ))}
        </div>
      </div>
    </Status>
  )
}
