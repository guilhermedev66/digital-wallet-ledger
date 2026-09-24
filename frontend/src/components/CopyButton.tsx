// Adapted from Spell UI's real Copy Button component (docs/design/spell/copy-button.json,
// fetched verbatim from https://spell.sh/r/copy-button.json).
//
// PRESERVED from the original: the crossfade transition between the Copy and Check
// icons (opacity + scale + blur, not a text swap), disabling the button while the
// "copied" state is showing, the `active:scale-[0.97]` press feedback, and the
// `size` prop (sm/default/lg). The icon paths themselves are also the original's -
// lucide-react's `CheckIcon`/`CopyIcon` SVG path data (ISC license), inlined below
// instead of importing the `lucide-react` package, consistent with this project's
// existing zero-icon-dependency pattern (see the previous hand-drawn SVG this
// file used to contain) and avoiding a new dependency for two paths.
//
// CHANGED from the original: `cn()`/Tailwind classes replaced with this project's
// CSS Modules + tokens.css; the "Copied" text status this file previously showed
// is gone (the icon crossfade communicates the same thing, per the source).
import { useEffect, useRef, useState, type ButtonHTMLAttributes } from 'react'
import styles from './CopyButton.module.css'

type Size = 'sm' | 'default' | 'lg'

const FEEDBACK_MS = 1500

const sizeMap: Record<Size, { button: string; icon: number }> = {
  sm: { button: styles.sizeSm, icon: 14 },
  default: { button: styles.sizeDefault, icon: 16 },
  lg: { button: styles.sizeLg, icon: 20 },
}

interface CopyButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'value'> {
  value: string
  label: string
  size?: Size
}

export function CopyButton({ value, label, size = 'default', className, onClick, ...rest }: CopyButtonProps) {
  const [copied, setCopied] = useState(false)
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)

  useEffect(() => () => clearTimeout(timeoutRef.current), [])

  async function handleClick(event: React.MouseEvent<HTMLButtonElement>) {
    try {
      await navigator.clipboard.writeText(value)
    } catch {
      // Clipboard API unavailable (insecure context, permission denied) - silently no-op;
      // the value is still visible and selectable in the DOM as a fallback.
      onClick?.(event)
      return
    }
    setCopied(true)
    clearTimeout(timeoutRef.current)
    timeoutRef.current = setTimeout(() => setCopied(false), FEEDBACK_MS)
    onClick?.(event)
  }

  const { button: sizeClass, icon: iconSize } = sizeMap[size]

  return (
    <button
      type="button"
      onClick={handleClick}
      aria-label={copied ? 'Copied' : `Copy ${label}`}
      disabled={copied}
      className={[styles.button, sizeClass, className].filter(Boolean).join(' ')}
      {...rest}
    >
      <span className={[styles.iconLayer, copied ? styles.iconVisible : styles.iconHidden].join(' ')}>
        <CheckIcon size={iconSize} />
      </span>
      <span className={[styles.iconLayer, styles.iconAbsolute, copied ? styles.iconHidden : styles.iconVisible].join(' ')}>
        <CopyIcon size={iconSize} />
      </span>
    </button>
  )
}

// lucide-react "check" icon path data (ISC license, https://lucide.dev), inlined.
function CheckIcon({ size }: { size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <path d="M20 6 9 17l-5-5" />
    </svg>
  )
}

// lucide-react "copy" icon path data (ISC license, https://lucide.dev), inlined.
function CopyIcon({ size }: { size: number }) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      <rect width="14" height="14" x="8" y="8" rx="2" ry="2" />
      <path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2" />
    </svg>
  )
}
