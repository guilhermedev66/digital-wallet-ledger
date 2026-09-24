// Adapted from Spell UI's real Kbd component (docs/design/spell/kbd.json,
// fetched verbatim from https://spell.sh/r/kbd.json).
//
// PRESERVED from the original: the key-name -> symbol lookup table (`cmd`/`command`
// -> "⌘", `ctrl`/`control` -> "⌃", arrows -> "←↓↑→", etc.) with a fallback of
// `key.toUpperCase()` for anything not in the map - so callers can pass semantic
// names ("cmd", "esc") instead of hand-picking unicode glyphs, and the "keycap"
// visual: a raised default state and a pressed-in state, both built from layered
// inset box-shadows rather than a flat border.
//
// CHANGED from the original: the live-keyboard-press highlighting
// (`react-hotkeys-hook`'s `useHotkeys`, which actually listens for the real key
// combo and lights up the badge when pressed) was dropped - this app's ⌘K listener
// already lives in `AppShell.tsx` and doesn't need a second, competing listener
// wired through every `<Kbd>` instance; the `active` prop is kept so a parent that
// *does* know its own pressed state can still opt in. Tailwind's `bg-background`/
// `text-foreground` and the box-shadow rgba literals were converted to this
// project's `tokens.css` custom properties.
import styles from './Kbd.module.css'

type KeyItem = string | { display: string; key: string }

const keySymbolMap: Record<string, string> = {
  command: '⌘',
  cmd: '⌘',
  control: '⌃',
  ctrl: '⌃',
  alt: '⌥',
  option: '⌥',
  space: '␣',
  arrowleft: '←',
  left: '←',
  arrowdown: '↓',
  down: '↓',
  arrowup: '↑',
  up: '↑',
  arrowright: '→',
  right: '→',
  enter: '↵',
  return: '↵',
}

function getKeyDisplay(item: KeyItem): string {
  const key = typeof item === 'string' ? item : item.display
  return keySymbolMap[key.toLowerCase()] ?? key.toUpperCase()
}

export function Kbd({ keys, active }: { keys: KeyItem[]; active?: boolean }) {
  return (
    <span className={styles.group} aria-hidden="true">
      {keys.map((item, i) => (
        <kbd key={i} className={[styles.key, active ? styles.keyActive : ''].filter(Boolean).join(' ')}>
          {getKeyDisplay(item)}
        </kbd>
      ))}
    </span>
  )
}
