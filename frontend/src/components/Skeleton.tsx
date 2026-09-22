import styles from './Skeleton.module.css'

export function Skeleton({ width = '100%' }: { width?: string | number }) {
  return <span className={styles.row} style={{ width, display: 'inline-block' }} aria-hidden="true" />
}

export function SkeletonRows({ count, width }: { count: number; width?: string | number }) {
  return (
    <div role="status" aria-label="Loading" style={{ display: 'grid', gap: 8 }}>
      {Array.from({ length: count }).map((_, i) => (
        <Skeleton key={i} width={width} />
      ))}
      <span className="visually-hidden">Loading…</span>
    </div>
  )
}
