import { AnimatePresence, motion, useReducedMotion } from 'motion/react'

/** Big typed value: every character pops in as it is typed. Decorative mirror of the real input. */
export function AnimatedDigits({
  text,
  placeholder,
  className,
}: {
  text: string
  placeholder: string
  className?: string
}) {
  const reduce = useReducedMotion()
  const shown = text.length > 0 ? text : placeholder
  const isPlaceholder = text.length === 0
  return (
    <span
      className={['amount inline-flex items-baseline justify-center', className].filter(Boolean).join(' ')}
      style={{ opacity: isPlaceholder ? 0.35 : 1, transition: 'opacity 0.2s' }}
      aria-hidden="true"
    >
      <AnimatePresence mode="popLayout" initial={false}>
        {shown.split('').map((ch, i) => (
          <motion.span
            key={`${i}-${ch}`}
            className="inline-block"
            initial={reduce ? { opacity: 0 } : { opacity: 0, y: 28, scale: 0.7 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={reduce ? { opacity: 0 } : { opacity: 0, y: -20, scale: 0.8 }}
            transition={{ type: 'spring', stiffness: 520, damping: 30 }}
          >
            {ch === ' ' ? ' ' : ch}
          </motion.span>
        ))}
      </AnimatePresence>
    </span>
  )
}
