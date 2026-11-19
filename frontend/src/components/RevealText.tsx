import { useReducedMotion } from 'motion/react'
import { BlurReveal } from './blur-reveal'
import { WordsStagger } from './words-stagger'

/** Spell BlurReveal title; plain text under prefers-reduced-motion. */
export function RevealTitle({
  children,
  as = 'h1',
  className,
  delay = 0,
}: {
  children: string
  as?: 'h1' | 'h2' | 'p'
  className?: string
  delay?: number
}) {
  const reduce = useReducedMotion()
  if (reduce) {
    const Tag = as
    return <Tag className={className}>{children}</Tag>
  }
  return (
    <BlurReveal as={as} className={className} delay={delay} speedReveal={1.6}>
      {children}
    </BlurReveal>
  )
}

/** Spell WordsStagger line; plain text under prefers-reduced-motion. */
export function RevealWords({
  children,
  className,
  delay = 0,
}: {
  children: string
  className?: string
  delay?: number
}) {
  const reduce = useReducedMotion()
  if (reduce) return <p className={className}>{children}</p>
  return (
    <WordsStagger className={className} delay={delay} stagger={0.05} speed={0.5}>
      {children}
    </WordsStagger>
  )
}
