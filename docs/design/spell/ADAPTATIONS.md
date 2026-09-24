# Spell UI — adaptation record (M8.2)

For each component: the official source consulted, the logic/API preserved, what
changed in the adaptation, and the final files. All four raw registry responses
are saved verbatim next to this file (`badge.json`, `copy-button.json`,
`pop-button.json`, `kbd.json`) — every claim below is checkable against them.

No global Tailwind/Radix/CVA was added to the project. `lucide-react` was not
added either — the two icon paths the real Copy Button uses are inlined as
static SVG (ISC-licensed, from `lucide-static`), consistent with this project's
pre-existing zero-icon-dependency pattern.

---

## Badge

**Official source:** https://spell.sh/docs/badge · `https://spell.sh/r/badge.json`
**Official install method:** `pnpm dlx shadcn@latest add @spell/badge` (shadcn/ui registry item; not run — see `README.md` in this folder)

**Logic preserved:**
- The two-axis `variant` + `size` API shape
- The composition pattern itself (base class + variant lookup + size lookup), reimplemented as a plain `styles[...]` lookup instead of `class-variance-authority`

**Changes made in the adaptation:**
- Tailwind utility strings → CSS Modules classes referencing `tokens.css` custom properties
- The original's 20-name raw color enum (`red`/`blue`/`green`/…) replaced with this project's existing 5-value semantic enum (`neutral`/`credit`/`debit`/`attention`/`danger`) — kept from the pre-M8.2 `Badge.tsx`, since a Tailwind color palette has no relationship to this product's tokens
- `asChild`/Radix `Slot` polymorphism dropped (no Radix); always renders a `<span>`

**Final files:** `frontend/src/components/Badge.tsx`, `frontend/src/components/Badge.module.css`
**Where used:** `ActivityPage.tsx`, `ReconciliationPage.tsx`, `TransferPage.tsx`

---

## Copy Button

**Official source:** https://spell.sh/docs/copy-button · `https://spell.sh/r/copy-button.json`
**Official install method:** `pnpm dlx shadcn@latest add @spell/copy-button`

**Logic preserved:**
- Icon-crossfade transition (opacity + scale + blur) between a "copy" and "check" icon, not a text swap
- Disabling the button while the "copied" state is showing
- `active:scale-[0.97]` press feedback
- The `size` prop (sm/default/lg)
- The actual icon shapes: lucide-react's `CheckIcon`/`CopyIcon` SVG path data (inlined, see below)

**Changes made in the adaptation:**
- Tailwind utility strings → CSS Modules classes
- `lucide-react` package import → the same two icons' path data inlined as static SVG components in `CopyButton.tsx` (ISC license, sourced from `lucide-static`), so no new npm dependency was added for two paths
- Kept this project's existing `label`/`value` prop names (the source uses `value` only, with `aria-label` hardcoded to a generic "Copy to clipboard"; this project's version keeps a caller-supplied `label` so the aria-label stays specific — "Copy wallet ID" vs "Copy transaction ID")

**Final files:** `frontend/src/components/CopyButton.tsx`, `frontend/src/components/CopyButton.module.css`
**Where used:** `WalletCard.tsx`, `ActivityPage.tsx`, `TransferPage.tsx`

---

## Pop Button

**Official source:** https://spell.sh/docs/pop-button · `https://spell.sh/r/pop-button.json`
**Official install method:** `pnpm dlx shadcn@latest add @spell/pop-button`

**Logic preserved:**
- The exact press mechanic: a thick bottom border (`border-b-4`) at rest that thins to `border-b-2` on press, combined with a bottom-anchored vertical squash (`scale-y-95`, `transform-origin: bottom`) — a genuinely different technique from a box-shadow compression
- The `color`/`size` two-axis variant API shape

**Changes made in the adaptation:**
- Tailwind utility strings → CSS Modules classes
- The original's 22-name raw color enum replaced with a 2-value semantic set (`accent`/`neutral`) matching this project's single-accent-per-view discipline (see `docs/design/refero/brex-design.md`'s "Do" list)
- `asChild`/Radix `Slot` dropped

**Final files:** `frontend/src/components/PopButton.tsx`, `frontend/src/components/PopButton.module.css` (new standalone component); the identical mechanic was also applied as a CSS-only treatment to `TransferPage.module.css`'s `.confirmButton`, replacing an earlier box-shadow-based approximation written before this real source was available (that button stays a `FlowButton`, not a `<PopButton>`, because it also needs FlowButton's in-flight loading behavior, which Pop Button doesn't have)
**Where used:** `DashboardPage.tsx` ("Create your first wallet" empty-state CTA, via `<PopButton>`); `TransferPage.tsx` (confirm button, via the shared CSS mechanic)

---

## Kbd

**Official source:** https://spell.sh/docs/kbd · `https://spell.sh/r/kbd.json`
**Official install method:** `pnpm dlx shadcn@latest add @spell/kbd`

**Logic preserved:**
- The key-name → symbol lookup table (`cmd`/`command` → "⌘", `ctrl`/`control` → "⌃", arrow names → "←↓↑→", etc.) with a `key.toUpperCase()` fallback for anything unmapped
- The "keycap" visual concept: a raised default state and a pressed-in state, both built from layered inset shadows rather than a flat border

**Changes made in the adaptation:**
- Tailwind's `bg-background`/`text-foreground` and the box-shadow rgba literals → this project's `tokens.css` custom properties (so the keycap look inherits both themes automatically, the same way every other component here does, instead of a second Tailwind `dark:` switch)
- The live-keyboard-press highlighting (`react-hotkeys-hook`'s `useHotkeys`, which listens for the real key combo) was dropped — this app's actual ⌘K listener already lives in `AppShell.tsx`; wiring a second, competing listener through every `<Kbd>` instance would be redundant. The `active` prop is kept so a parent that already knows its own pressed state can still opt in
- Call sites (`AppShell.tsx`, `CommandPalette.tsx`) updated to pass semantic key names (`'cmd'`, `'up'`, `'down'`, `'enter'`) instead of hand-picked unicode glyphs, so the preserved lookup table is actually exercised, not dead code

**Final files:** `frontend/src/components/Kbd.tsx`, `frontend/src/components/Kbd.module.css`
**Where used:** `AppShell.tsx`, `CommandPalette.tsx`
