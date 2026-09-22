import type { ReactNode } from 'react'
import styles from './EmptyState.module.css'

export function EmptyState({
  title,
  description,
  action,
}: {
  title: string
  description?: string
  action?: ReactNode
}) {
  return (
    <div className={styles.container}>
      <span className={styles.title}>{title}</span>
      {description && <span className={styles.description}>{description}</span>}
      {action}
    </div>
  )
}
