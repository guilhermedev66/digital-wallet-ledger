// Adapted from Spell UI's real Badge component (docs/design/spell/badge.json,
// fetched verbatim from https://spell.sh/r/badge.json - the same registry endpoint
// `pnpm dlx shadcn@latest add @spell/badge` pulls from).
//
// PRESERVED from the original: the two-axis `variant` + `size` API shape, the
// `forwardRef`-free-but-ref-forwarding-capable span/div-as-badge pattern, and the
// cva mental model itself (a base class string + a variant lookup + a size lookup,
// composed together) - reimplemented here as a plain `styles[...]` lookup instead
// of `class-variance-authority`, since CVA is a Tailwind-class-string composer and
// this project has no Tailwind.
//
// CHANGED from the original: the original's `variant` enum is 20 raw Tailwind
// color names (red/blue/green/...) with no product meaning. This project already
// has a semantic variant set tied to "silence = good, accent = look here"
// (M8_1_VISUAL_REFERENCES.md #3, Brex) - `neutral` for routine facts, `attention`/
// `danger` only when something needs the user's eye - so the color-name enum was
// replaced with that semantic one rather than importing 20 meaningless options.
// `asChild`/Radix `Slot` polymorphism was dropped (Radix is out of scope per this
// milestone's integration constraint); every badge here is always a `<span>`.
import type { HTMLAttributes } from 'react'
import styles from './Badge.module.css'

type Variant = 'neutral' | 'credit' | 'debit' | 'attention' | 'danger'
type Size = 'default' | 'sm'

interface BadgeProps extends HTMLAttributes<HTMLSpanElement> {
  variant?: Variant
  size?: Size
}

export function Badge({ children, variant = 'neutral', size = 'default', className, ...rest }: BadgeProps) {
  return (
    <span
      className={[styles.badge, styles[variant], styles[`size-${size}`], className]
        .filter(Boolean)
        .join(' ')}
      {...rest}
    >
      {children}
    </span>
  )
}
