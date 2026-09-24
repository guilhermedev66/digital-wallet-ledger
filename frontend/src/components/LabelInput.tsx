import { useId, type InputHTMLAttributes, type ReactNode } from 'react'
import styles from './LabelInput.module.css'

interface LabelInputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string
  hint?: string
  error?: string
  prefixSlot?: ReactNode
  suffixSlot?: ReactNode
  scale?: 'default' | 'lg'
  mono?: boolean
}

export function LabelInput({
  label,
  hint,
  error,
  prefixSlot,
  suffixSlot,
  scale = 'default',
  mono = false,
  id,
  className,
  ...rest
}: LabelInputProps) {
  const generatedId = useId()
  const inputId = id ?? generatedId
  const hintId = hint ? `${inputId}-hint` : undefined
  const errorId = error ? `${inputId}-error` : undefined

  return (
    <div className={[styles.field, className].filter(Boolean).join(' ')}>
      <label className={styles.label} htmlFor={inputId}>
        {label}
      </label>
      <div
        className={[
          styles.well,
          scale === 'lg' ? styles.wellLg : '',
          error ? styles.wellError : '',
        ]
          .filter(Boolean)
          .join(' ')}
      >
        {prefixSlot && <span className={styles.prefix}>{prefixSlot}</span>}
        <input
          id={inputId}
          className={[styles.input, scale === 'lg' ? styles.inputLg : '', mono ? 'mono' : '']
            .filter(Boolean)
            .join(' ')}
          aria-invalid={Boolean(error) || undefined}
          aria-describedby={[hintId, errorId].filter(Boolean).join(' ') || undefined}
          {...rest}
        />
        {suffixSlot && <span className={styles.suffix}>{suffixSlot}</span>}
      </div>
      {hint && !error && (
        <span id={hintId} className={styles.hint}>
          {hint}
        </span>
      )}
      {error && (
        <span id={errorId} role="alert" className={styles.error}>
          {error}
        </span>
      )}
    </div>
  )
}
