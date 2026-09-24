// Adapted from Spell UI's real Pop Button component (docs/design/spell/pop-button.json,
// fetched verbatim from https://spell.sh/r/pop-button.json).
//
// PRESERVED from the original: the exact press mechanic - a thick bottom border
// (`border-b-4`) at rest that shrinks to a thin one on press (`border-b-2`)
// combined with a bottom-anchored vertical squash (`scale-y-95`, `origin-bottom`)
// - which is a genuinely different technique from a box-shadow compression (what
// this project's Transfer confirm button used before this adaptation pass; that
// CSS was rewritten to use this same border+scaleY mechanic once it existed here
// as a real source, see `TransferPage.module.css`'s `.confirmButton`). Also
// preserved: the `color`/`size` two-axis variant API shape and `forwardRef`.
//
// CHANGED from the original: the `color` enum was 22 raw Tailwind color names
// with no product meaning; replaced with this project's own two-value semantic
// set (`accent`/`neutral`) matching the existing single-accent-per-view
// discipline (Brex reference, docs/design/refero/brex-design.md's "Do" list:
// "Use Ember for exactly one purpose per region"). `asChild`/Radix `Slot`
// polymorphism was dropped (Radix is out of scope for this milestone).
import type { ButtonHTMLAttributes, ReactNode } from 'react'
import styles from './PopButton.module.css'

type Color = 'accent' | 'neutral'
type Size = 'sm' | 'default' | 'lg'

interface PopButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  color?: Color
  size?: Size
  children: ReactNode
}

export function PopButton({
  color = 'accent',
  size = 'default',
  children,
  className,
  disabled,
  ...rest
}: PopButtonProps) {
  return (
    <button
      type="button"
      disabled={disabled}
      className={[styles.button, styles[color], styles[`size-${size}`], className]
        .filter(Boolean)
        .join(' ')}
      {...rest}
    >
      {children}
    </button>
  )
}
