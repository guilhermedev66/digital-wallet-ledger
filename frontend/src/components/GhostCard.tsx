import { motion, useReducedMotion } from 'motion/react'

/** Translucent placeholder wallet card that floats slowly (empty-state hint of what you'll get). */
export function GhostCard() {
  const reduce = useReducedMotion()
  return (
    <motion.div
      aria-hidden="true"
      className="relative mt-4 aspect-[1.586/1] w-[min(300px,70vw)] rounded-[24px] border border-dashed border-[var(--color-border-strong)] bg-[linear-gradient(135deg,rgba(74,33,239,0.28),rgba(121,239,189,0.12))] opacity-70"
      animate={reduce ? undefined : { y: [0, -12, 0], rotate: [-1.5, 1.5, -1.5] }}
      transition={{ duration: 6, ease: 'easeInOut', repeat: Infinity }}
    >
      <span className="absolute left-5 top-5 h-2.5 w-20 rounded-full bg-[var(--color-text)] opacity-20" />
      <span className="absolute right-5 top-5 h-5 w-12 rounded-full bg-[var(--color-text)] opacity-15" />
      <span className="absolute left-5 top-1/2 h-7 w-10 -translate-y-1/2 rounded-md bg-[var(--color-text)] opacity-20" />
      <span className="absolute bottom-5 left-5 h-2.5 w-28 rounded-full bg-[var(--color-text)] opacity-20" />
      <span className="absolute bottom-5 right-5 h-2.5 w-10 rounded-full bg-[var(--color-text)] opacity-15" />
    </motion.div>
  )
}
