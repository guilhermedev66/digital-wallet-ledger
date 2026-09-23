import { useEffect, useRef, useState } from 'react'
import styles from './CopyButton.module.css'

const FEEDBACK_MS = 1400

export function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timeoutRef.current), [])

  async function handleClick() {
    try {
      await navigator.clipboard.writeText(value)
    } catch {
      // Clipboard API unavailable (insecure context, permission denied) - silently no-op;
      // the value is still visible and selectable in the DOM as a fallback.
      return
    }
    setCopied(true)
    clearTimeout(timeoutRef.current)
    timeoutRef.current = setTimeout(() => setCopied(false), FEEDBACK_MS)
  }

  return (
    <button
      type="button"
      className={styles.button}
      onClick={handleClick}
      aria-label={`Copy ${label}`}
    >
      {copied ? (
        <span className={styles.status} role="status">
          Copied
        </span>
      ) : (
        <svg viewBox="0 0 16 16" width="12" height="12" aria-hidden="true">
          <path
            fill="none"
            stroke="currentColor"
            strokeWidth="1.3"
            d="M6 2h6.5a.5.5 0 0 1 .5.5V11M3.5 5h6.5a.5.5 0 0 1 .5.5V13a.5.5 0 0 1-.5.5h-6.5a.5.5 0 0 1-.5-.5V5.5a.5.5 0 0 1 .5-.5Z"
          />
        </svg>
      )}
    </button>
  )
}
